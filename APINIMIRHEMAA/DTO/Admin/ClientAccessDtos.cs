namespace APINIMIRHEMAA.DTO.Admin
{

    public sealed class ClientAccessEmployeeDto
    {

        public int User_ID { get; set; }

        public int Employee_ID { get; set; }

        public string Employee_Name { get; set; } = "";

        public string Username { get; set; } = "";

        public bool IsActive { get; set; }
    }


    public sealed class ClientAccessGrantDto
    {

        public int Client_ID { get; set; }

        public string Client_Name { get; set; } = "";

        public int User_ID { get; set; }

        public string Employee_Name { get; set; } = "";

        public string Username { get; set; } = "";

        public DateTime GrantedAtUtc { get; set; }
    }


    public sealed class SaveClientAccessGrantDto
    {

        public int Client_ID { get; set; }

        public int User_ID { get; set; }
    }


    public sealed class SaveClientAccessGrantsDto
    {

        public int OwnerUser_ID { get; set; }

        public List<int> Client_IDs { get; set; } = new();

        public List<int> User_IDs { get; set; } = new();
    }
}
