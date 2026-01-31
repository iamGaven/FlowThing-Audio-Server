namespace Audio.Models
{
    /// <summary>
    /// Response model for API information at the root endpoint.
    /// </summary>
    public class ApiInfoResponse
    {
        /// <summary>
        /// Gets or sets the welcome message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Swagger UI URL.
        /// </summary>
        public string SwaggerUI { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the test page information.
        /// </summary>
        public string TestPage { get; set; } = string.Empty;
    }
}
