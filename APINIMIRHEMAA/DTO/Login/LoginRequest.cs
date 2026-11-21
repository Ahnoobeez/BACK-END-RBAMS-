namespace APINIMIRHEMAA.DTO.Login
{
    // Defines the login request body exchanged through the API.
    public class LoginRequest
    {
        // Carries username for the related API operation.
        public string Username { get; set; }
        // Carries password for the related API operation.
        public string Password { get; set; }
    }
}
