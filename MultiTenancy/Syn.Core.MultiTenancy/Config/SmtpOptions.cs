namespace Syn.Core.MultiTenancy.DI
{
    /// <summary>
    /// SMTP configuration options.
    /// </summary>
    public sealed class SmtpOptions
    {
        /// <summary>
        /// SMTP host name (e.g., smtp.tenant1.myapp.com).
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// SMTP port number.
        /// </summary>
        public int Port { get; set; } = 25;

        /// <summary>
        /// Whether to use SSL for SMTP.
        /// </summary>
        public bool UseSsl { get; set; } = true;

        /// <summary>
        /// Display name of the sender.
        /// </summary>
        public string SenderName { get; set; } = "Default";
    }
}
