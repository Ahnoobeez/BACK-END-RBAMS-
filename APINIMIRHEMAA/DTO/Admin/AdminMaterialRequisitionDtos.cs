using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.Admin
{
    public class AdminMaterialRequisitionRequest
    {
        [Required, MaxLength(100)]
        public string Control_Number { get; set; } = "";

        [Range(1, int.MaxValue)]
        public int Material_ID { get; set; }

        [MaxLength(200)]
        public string? Supplier { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Range(0, int.MaxValue)]
        public int Unit_Cost { get; set; }
    }

    public class AdminMaterialRequisitionOption
    {
        public int Material_ID { get; set; }
        public string Item_Name { get; set; } = "";
        public string Code_Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public int Available_Purchased { get; set; }
    }
}
