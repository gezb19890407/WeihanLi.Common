﻿using System;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// Prvodides extensions for <see cref="IConfiguration"/> instances.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// A regex which matches tokens in the following format: $(Item:Sub1:Sub2).
        /// inspired by https://github.com/henkmollema/ConfigurationPlaceholders
        /// </summary>
        private static readonly Regex ConfigPlaceholderRegex = new Regex(@"\$\(([A-Za-z0-9:_]+?)\)");

        /// <summary>
        /// Replaces the placeholders in the specified <see cref="IConfiguration"/> instance.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> instance to replace placeholders in.</param>
        /// <returns>The given <see cref="IConfiguration"/> instance.</returns>
        public static IConfiguration ReplacePlaceholders(this IConfiguration configuration)
        {
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (string.IsNullOrEmpty(kvp.Value))
                {
                    // Skip empty configuration values.
                    continue;
                }

                // Replace placeholders in the configuration value.
                var result = ConfigPlaceholderRegex.Replace(kvp.Value, match =>
                {
                    if (!match.Success)
                    {
                        // Return the original value.
                        return kvp.Value;
                    }

                    if (match.Groups.Count != 2)
                    {
                        // There is a match, but somehow no group for the placeholder.
                        throw new InvalidConfigurationPlaceholderException(match.ToString());
                    }

                    var placeholder = match.Groups[1].Value;
                    if (placeholder.StartsWith(":") || placeholder.EndsWith(":"))
                    {
                        // Placeholders cannot start or end with a colon.
                        throw new InvalidConfigurationPlaceholderException(placeholder);
                    }

                    // Return the value in the configuration instance.
                    return configuration[placeholder];
                });

                // Replace the value in the configuration instance.
                configuration[kvp.Key] = result;
            }

            return configuration;
        }

        private class InvalidConfigurationPlaceholderException : InvalidOperationException
        {
            public InvalidConfigurationPlaceholderException(string placeholder) : base($"Invalid configuration placeholder: '{placeholder}'.")
            {
            }
        }
    }
}
