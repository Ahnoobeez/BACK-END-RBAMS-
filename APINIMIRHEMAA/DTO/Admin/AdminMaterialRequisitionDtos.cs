using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.Admin
{
    // Defines the admin material requisition request body exchanged through the API.
    public class AdminMaterialRequisitionRequest
    {
        [Required, MaxLength(100)]
        // Supplies the control number used to identify the related record.
        public string Control_Number { get; set; } = "";

        [Range(1, int.MaxValue)]
        // Identifies the related material record.
        public int Material_ID { get; set; }

        [MaxLength(200)]
        // Carries supplier for the related API operation. This value is optional. The maximum-length annotation limits accepted text length.
        public string? Supplier { get; set; }

        [Range(1, int.MaxValue)]
        // Carries quantity for the related API operation. The range annotation constrains its numeric value.
        public int Quantity { get; set; }

        [Range(0, int.MaxValue)]
        // Financial unit cost recorded for this admin material requisition request.
        public int Unit_Cost { get; set; }
    }

    // Defines the admin material requisition response or transfer shape exchanged through the API.
    public class AdminMaterialRequisitionOption
    {
        // Identifies the related material record.
        public int Material_ID { get; set; }
        // Supplies the item name used to identify the related record.
        public string Item_Name { get; set; } = "";
        // Supplies the code name used to identify the related record.
        public string Code_Name { get; set; } = "";
        // Carries unit for the related API operation.
        public string Unit { get; set; } = "";
        // Carries available purchased for the related API operation.
        public int Available_Purchased { get; set; }
    }
}
