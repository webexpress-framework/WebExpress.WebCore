using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebExpress.WebCore.WebEmail;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.Test.Manager
{
    /// <summary>
    /// Exercises the package-free SMTP transport boundaries without contacting external servers.
    /// </summary>
    public sealed class UnitTestSmtpEmailProvider
    {
        /// <summary>Checks authentication command order and keeps a rejected identity from reaching the envelope.</summary>
        /// <param name="mechanism">The only authentication mechanism advertised by the test server.</param>
        /// <param name="accepted">Whether the server accepts the authentication response.</param>
        /// <returns>A task completing after the command transcript has been checked.</returns>
        [Theory]
        [InlineData("PLAIN", true)]
        [InlineData("LOGIN", true)]
        [InlineData("PLAIN", false)]
        [InlineData("LOGIN", false)]
        public async Task AuthenticationRequiresAdvertisedMechanismAndAcceptance(string mechanism, bool accepted)
        {
            // arrange
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var responses = "334 challenge\r\n" + (mechanism == "LOGIN" ? "334 password\r\n" : "")
                + (accepted ? "235 authenticated\r\n250 sender\r\n250 recipient\r\n354 data\r\n250 accepted\r\n" : "535 rejected\r\n");
            var transcript = ExchangeAsync(listener, responses, deadline.Token);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
            using var message = new MailMessage("sender@example.org", "receiver@example.org");
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString("body", Encoding.UTF8, "text/plain"));
            var profile = new EmailProfileSettings { UserName = "account", Password = "secret" };

            // act
            var error = await Record.ExceptionAsync(() => SmtpEmailProvider.SubmitAsync(client.GetStream(), message,
                profile, ["localhost", "AUTH " + mechanism], deadline.Token));
            client.Client.Shutdown(SocketShutdown.Send);
            var commands = await transcript;

            // validation
            if (accepted) { Assert.Null(error); }
            else { Assert.IsType<SmtpException>(error); }
            Assert.StartsWith("AUTH " + mechanism + "\r\n", commands);
            Assert.Equal(accepted, commands.Contains("MAIL FROM:"));
            Assert.DoesNotContain("secret", commands);
            if (mechanism == "PLAIN")
            {
                Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("\0account\0secret")), commands);
            }
            else
            {
                Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("account")) + "\r\n"
                    + Convert.ToBase64String(Encoding.UTF8.GetBytes("secret")), commands);
            }
        }

        /// <summary>Rejects an untrusted certificate for both supported TLS connection modes.</summary>
        /// <param name="security">The point at which TLS must begin.</param>
        /// <returns>A task completing after the TLS trust failure has been observed.</returns>
        [Theory]
        [InlineData(EmailSecurity.StartTls)]
        [InlineData(EmailSecurity.SslOnConnect)]
        public async Task TlsDoesNotTrustAnUnregisteredCertificate(EmailSecurity security)
        {
            // arrange
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
            using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            // schannel requires an imported key container for a server certificate
            using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
            var worker = RejectTlsAsync(listener, certificate, security, deadline.Token);
            using var message = new MailMessage("sender@example.org", "receiver@example.org");
            var profile = new EmailProfileSettings
            {
                Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                Security = security, UserName = "account", Password = "secret"
            };

            // act
            var error = await Record.ExceptionAsync(() => new SmtpEmailProvider().SendAsync(message, profile, deadline.Token));
            var beforeTls = await worker;

            // validation
            Assert.True(error is AuthenticationException, error?.ToString());
            Assert.DoesNotContain("AUTH", beforeTls);
            Assert.DoesNotContain("MAIL FROM", beforeTls);
            Assert.Equal(security == EmailSecurity.StartTls, beforeTls.Contains("STARTTLS"));
        }

        /// <summary>Bounds actual socket reads and preserves caller cancellation while waiting for the greeting.</summary>
        /// <returns>A task completing after the cancelled connection has been closed.</returns>
        [Fact]
        public async Task CancellationInterruptsAStalledServer()
        {
            // arrange
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            using var message = new MailMessage("sender@example.org", "receiver@example.org");
            var profile = new EmailProfileSettings
            {
                Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, Security = EmailSecurity.None
            };
            var sending = new SmtpEmailProvider().SendAsync(message, profile, cancellation.Token);
            using var peer = await listener.AcceptTcpClientAsync(deadline.Token);

            // act
            cancellation.Cancel();
            var error = await Record.ExceptionAsync(async () => await sending.WaitAsync(deadline.Token));

            // validation
            Assert.IsAssignableFrom<OperationCanceledException>(error);
        }

        /// <summary>Keeps malformed or excessive server replies from advancing the SMTP conversation.</summary>
        /// <param name="greeting">The invalid response sent by the server.</param>
        /// <returns>A task completing after the bounded parser rejects the greeting.</returns>
        [Theory]
        [InlineData("220-first\r\n250 wrong code\r\n")]
        [InlineData("220!wrong delimiter\r\n")]
        [InlineData("220 wrong newline\n")]
        [InlineData("oversized")]
        public async Task InvalidGreetingNeverStartsAnEnvelope(string greeting)
        {
            // arrange
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var transcript = ExchangeAsync(listener, greeting == "oversized" ? "220 " + new string('x', 512) + "\r\n" : greeting, deadline.Token);
            using var message = new MailMessage("sender@example.org", "receiver@example.org");
            var profile = new EmailProfileSettings
            {
                Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, Security = EmailSecurity.None
            };

            // act
            var error = await Record.ExceptionAsync(() => new SmtpEmailProvider().SendAsync(message, profile, deadline.Token));
            var commands = await transcript;

            // validation
            Assert.IsType<SmtpException>(error);
            Assert.Empty(commands);
        }

        /// <summary>Supplies deterministic replies and records the commands without emulating client code.</summary>
        /// <param name="listener">The isolated local test listener.</param>
        /// <param name="responses">The ordered SMTP replies consumed by the client.</param>
        /// <param name="token">The bounded test deadline.</param>
        /// <returns>The complete command transcript after the client closes its send direction.</returns>
        private static async Task<string> ExchangeAsync(TcpListener listener, string responses, CancellationToken token)
        {
            using var peer = await listener.AcceptTcpClientAsync(token);
            await using var stream = peer.GetStream();
            await stream.WriteAsync(Encoding.UTF8.GetBytes(responses), token);
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var transcript = new StringBuilder();
            var buffer = new char[1024];
            try
            {
                int count;
                while ((count = await reader.ReadAsync(buffer, token)) > 0) { transcript.Append(buffer, 0, count); }
            }
            catch (IOException) when (responses.Length > 512)
            {
                // closing with unread excess reply bytes can reset the loopback connection
            }
            return transcript.ToString();
        }

        /// <summary>Offers a real TLS handshake with an isolated certificate that the platform must reject.</summary>
        /// <param name="listener">The isolated local test listener.</param>
        /// <param name="certificate">The untrusted certificate created for this test.</param>
        /// <param name="security">The requested implicit or explicit TLS mode.</param>
        /// <param name="token">The bounded test deadline.</param>
        /// <returns>The commands observed before the TLS handshake.</returns>
        private static async Task<string> RejectTlsAsync(TcpListener listener, X509Certificate2 certificate,
            EmailSecurity security, CancellationToken token)
        {
            using var peer = await listener.AcceptTcpClientAsync(token);
            await using var stream = peer.GetStream();
            var commands = "";
            if (security == EmailSecurity.StartTls)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("220 localhost\r\n"), token);
                commands += await reader.ReadLineAsync(token);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("250-localhost\r\n250 STARTTLS\r\n"), token);
                commands += await reader.ReadLineAsync(token);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("220 upgrade\r\n"), token);
            }
            using var secure = new SslStream(stream, true);
            try
            {
                await secure.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, token);
                var data = new byte[1];
                Assert.Equal(0, await secure.ReadAsync(data, token));
            }
            catch (Exception exception) when (exception is IOException or AuthenticationException)
            {
                // the client can report its trust failure as a tls alert or close the transport
                TestContext.Current.TestOutputHelper?.WriteLine(exception.ToString());
            }
            return commands;
        }
    }
}
