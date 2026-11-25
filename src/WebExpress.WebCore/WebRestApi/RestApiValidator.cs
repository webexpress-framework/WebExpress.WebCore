using System;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebRestApi
{
    /// <summary>
    /// Provides a fluent API for validating REST API request parameters.
    /// </summary>
    public class RestApiValidator
    {
        private readonly IRequest _request;
        private readonly RestApiValidationResult _result = new();
        private bool _currentCondition = true;

        /// <summary>
        /// Gets the result of the REST API validation.
        /// </summary>
        public RestApiValidationResult Result => _result;

        /// <summary>
        /// Gets a value indicating whether the current result is valid.
        /// </summary>
        public bool IsValid => _result.IsValid;

        /// <summary>
        /// Initializes a new instance of the class with the specified request.
        /// </summary>
        /// <param name="request">The request to be validated.</param>
        public RestApiValidator(IRequest request)
        {
            _request = request;
        }

        /// <summary>
        /// Specifies a condition that determines whether the current validation logic 
        /// should be applied.
        /// </summary>
        /// <param name="condition">
        /// A function that takes a request and returns true if the condition is
        /// met; otherwise, false.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator When(Func<IRequest, bool> condition)
        {
            _currentCondition = condition(_request);
            return this;
        }

        /// <summary>
        /// Ensures that the specified parameter is present and not null, empty, 
        /// or whitespace in the request.
        /// </summary>
        /// <remarks>
        /// If the parameter is missing or invalid, an error is added to the 
        /// validation result with the specified or default message. This method 
        /// does nothing if the current validation condition is not met.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator Require(string parameter, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.required", parameter),
                    parameter,
                    "REQUIRED"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter's value does not exceed the given 
        /// maximum length.
        /// </summary>
        /// <remarks>
        /// If the parameter's value exceeds the specified maximum length, an 
        /// error is added to the validation result. This method does nothing 
        /// if the current validation condition is not met.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="max">The maximum allowed length for the parameter's value.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator MaxLength(string parameter, int max, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!string.IsNullOrWhiteSpace(value) && value.Length > max)
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.too_long", parameter, max.ToString()),
                    parameter,
                    "TOO_LONG"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter's value meets the minimum 
        /// length requirement.
        /// </summary>
        /// <remarks>
        /// If the parameter's value is null, empty, or consists only of 
        /// whitespace, this validation is skipped. If the value is shorter 
        /// than the specified minimum length, an error is added to the
        /// validation result.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="min">The minimum allowable length for the parameter's value.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator MinLength(string parameter, int min, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;

            if (string.IsNullOrWhiteSpace(value) && min > 0)
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.too_short", parameter, min.ToString()),
                    parameter,
                    "TOO_SHORT"
                );
            }
            else if (!string.IsNullOrWhiteSpace(value) && value.Length < min)
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.too_short", parameter, min.ToString()),
                    parameter,
                    "TOO_SHORT"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the value of a specified request parameter matches a 
        /// given regular expression pattern.
        /// </summary>
        /// <remarks>
        /// If the parameter's value is null, empty, or consists only of 
        /// whitespace, the validation is skipped. If the value does not match 
        /// the specified pattern, an error is added to the validation result.
        /// </remarks>
        /// <param name="parameter">The name of the request parameter to validate.</param>
        /// <param name="pattern">
        /// The regular expression pattern to match against the parameter's value.
        /// </param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator Regex(string parameter, string pattern, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!string.IsNullOrWhiteSpace(value) && !MatchesWithin(value, pattern))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.regex_mismatch", parameter),
                    parameter,
                    "REGEX_MISMATCH"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter's value is within the given range.
        /// </summary>
        /// <remarks>
        /// If the parameter's value is not a valid integer or falls outside the 
        /// specified range, an error is added to the validation result. This 
        /// method does nothing if the current  validation condition is not active.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="min">The minimum allowable value for the parameter.</param>
        /// <param name="max">The maximum allowable value for the parameter.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator Range(string parameter, int min, int max, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (int.TryParse(value, out var number) && (number < min || number > max))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.out_of_range", parameter, min.ToString(), max.ToString()),
                    parameter,
                    "OUT_OF_RANGE"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter is a valid integer.
        /// </summary>
        /// <remarks>
        /// If the parameter value is not a valid integer, an error is added to 
        /// the validation result  with the specified or default error message. If 
        /// the current condition is false, the method  does not perform validation 
        /// and immediately returns the current instance.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator IsInt(string parameter, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!int.TryParse(value, out _))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.not_integer", parameter),
                    parameter,
                    "NOT_INTEGER"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter contains a valid email address.
        /// </summary>
        /// <remarks>
        /// This method checks if the value of the specified parameter is a valid 
        /// email address using a regular expression. If the value is invalid, an 
        /// error is added to the validation result. The validation is skipped if 
        /// the current condition is not met.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator Email(string parameter, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!string.IsNullOrWhiteSpace(value) && !System.Text.RegularExpressions.Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.invalid_email", parameter),
                    parameter,
                    "INVALID_EMAIL"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter in the request equals the expected value.
        /// </summary>
        /// <remarks>
        /// This method performs a case-insensitive comparison of the parameter's value 
        /// against the expected value. If the values do not match, an error is added to 
        /// the validation result.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="expected">The expected value of the parameter.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator EqualTo(string parameter, string expected, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.mismatch", parameter, expected),
                    parameter,
                    "MISMATCH"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter's value is not equal to the provided value.
        /// </summary>
        /// <remarks>
        /// This method performs a case-insensitive comparison of the parameter's value
        /// against the specified value. If the values are equal, an error is added 
        /// to the validation result.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="notExpected">The value that the parameter's value must not match.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator NotEqualTo(string parameter, string notExpected, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (string.Equals(value, notExpected, StringComparison.OrdinalIgnoreCase))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.must_differ", parameter, notExpected),
                    parameter,
                    "MUST_DIFFER"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the value of the specified parameter starts with the given prefix.
        /// </summary>
        /// <remarks>
        /// If the parameter value does not start with the specified prefix, an error 
        /// is added to the validation result. This method does nothing if the current 
        /// validation condition is not active.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="prefix">The prefix that the parameter value must start with.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator StartsWith(string parameter, string prefix, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith(prefix))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.prefix_mismatch", parameter, prefix),
                    parameter,
                    "PREFIX_MISMATCH"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter's value is one of the allowed values.
        /// </summary>
        /// <remarks>
        /// If the parameter's value is not one of the allowed values, an error is 
        /// added to the validation result. This method does nothing if the current 
        /// condition is not met.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="allowedValues">
        /// An array of allowed values for the parameter. Validation is 
        /// case-insensitive.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator In(string parameter, params string[] allowedValues)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!string.IsNullOrWhiteSpace(value) &&
                !allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                _result.Add(
                    I18N.Translate(_request, "webexpress.webcore:validation.invalid_choice", parameter, string.Join(", ", allowedValues)),
                    parameter,
                    "INVALID_CHOICE"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter contains the given text.
        /// </summary>
        /// <remarks>
        /// If the parameter's value is null, empty, or does not contain the 
        /// specified text, an error is added to the validation result.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="text">The text that the parameter's value must contain.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator Contains(string parameter, string text, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (string.IsNullOrWhiteSpace(value) || !value.Contains(text))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.missing_fragment", parameter, text),
                    parameter,
                    "MISSING_FRAGMENT"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the value of the specified parameter ends with the given suffix.
        /// </summary>
        /// <remarks>
        /// If the parameter value does not end with the specified suffix, an error 
        /// is added to the validation result. This method does nothing if the current 
        /// validation condition is not met.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="suffix">The required suffix that the parameter value must end with.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator EndsWith(string parameter, string suffix, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!string.IsNullOrWhiteSpace(value) && !value.EndsWith(suffix))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.suffix_mismatch", parameter, suffix),
                    parameter,
                    "SUFFIX_MISMATCH"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter value matches a valid value of 
        /// the specified enumeration type.
        /// </summary>
        /// <remarks>
        /// This method checks whether the value of the specified parameter can be 
        /// parsed as a valid value of the given enumeration type. If the value is 
        /// invalid, an error is added to the validation result.
        /// </remarks>
        /// <typeparam name="T">
        /// The enumeration type to validate against. Must be a non-nullable enum.
        /// </typeparam>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator MatchesEnum<T>(string parameter, string message = null)
            where T : struct, Enum
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!Enum.TryParse<T>(value, true, out _))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.invalid_enum", parameter, typeof(T).Name),
                    parameter,
                    "INVALID_ENUM"
                );
            }

            return this;
        }

        /// <summary>
        /// Validates that the specified parameter is a valid date.
        /// </summary>
        /// <remarks>
        /// If the parameter value cannot be parsed as a valid date, an error 
        /// is added to the validation result. This method does nothing if the 
        /// current condition is not met.
        /// </remarks>
        /// <param name="parameter">The name of the parameter to validate.</param>
        /// <param name="message">
        /// An optional custom error message to include if the validation fails. 
        /// If not provided, a default message will be used.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator IsDate(string parameter, string message = null)
        {
            if (!_currentCondition)
            {
                return this;
            }

            var value = _request.GetParameter(parameter)?.Value;
            if (!DateTime.TryParse(value, out _))
            {
                _result.Add(
                    message ?? I18N.Translate(_request, "webexpress.webcore:validation.invalid_date", parameter),
                    parameter,
                    "INVALID_DATE"
                );
            }

            return this;
        }

        /// <summary>
        /// Adds a custom validation rule to the current request.
        /// </summary>
        /// <remarks>
        /// This method only applies the custom validation rule if the current 
        /// condition is active. If the condition evaluates to false, the 
        /// specified error message, parameter, and code are added to the 
        /// validation result.
        /// </remarks>
        /// <param name="condition">
        /// A function that evaluates the request and returns true if the 
        /// condition is met; otherwise, false.
        /// </param>
        /// <param name="message">
        /// The error message to associate with the validation failure if 
        /// the condition is not met.
        /// </param>
        /// <param name="parameter">
        /// The name of the parameter associated with the validation failure, 
        /// or null if not applicable. This parameter is optional.
        /// </param>
        /// <param name="code">
        /// A custom error code to associate with the validation failure. 
        /// Defaults to "CUSTOM" if not specified.
        /// </param>
        /// <returns>The current instance, allowing for method chaining.</returns>
        public RestApiValidator Custom(Func<IRequest, bool> condition, string message, string parameter = null, string code = "CUSTOM")
        {
            if (!_currentCondition)
            {
                return this;
            }

            if (!condition(_request))
            {
                _result.Add(message, parameter, code);
            }

            return this;
        }

        /// <summary>
        /// Matches a client value against a pattern, counting a match that runs too long as a
        /// mismatch: the value is the client's, and a pattern that backtracks badly on it must not
        /// hold the request thread.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="pattern">The pattern.</param>
        /// <returns>True when the value matches in time.</returns>
        private static bool MatchesWithin(string value, string pattern)
        {
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(value, pattern, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(250));
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
