using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    // Represents the inventory record stored or used by the application.
    public class Inventory : ICreatedByRecord
    {
        // Stores created by on this inventory.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related inventory record.
        public int Inventory_ID { get; set; }
        // Financial beginning balance recorded for this inventory.
        public int? Beginning_Balance { get; set; }
        // Stores buffer on this inventory.
        public int? Buffer { get; set; }
        // Stores purchased on this inventory.
        public int? Purchased { get; set; }
        // Stores request on this inventory.
        public int? Request { get; set; }
        // Stores returned on this inventory.
        public int? Returned { get; set; }
        // Financial balance recorded for this inventory.
        public int? Balance { get; set; }
        // Current workflow status for this inventory.
        public string? Status { get; set; }
        // Identifies the related material record.
        public int Material_ID { get; set; }

        // Financial request balance recorded for this inventory.
        public int? Request_Balance { get; set; }
        // Stores request purchased on this inventory.
        public int? Request_Purchased { get; set; }
        // Stores request buffer on this inventory.
        public int? Request_Buffer { get; set; }

        [NotMapped]
        // Financial available balance recorded for this inventory.
        public int? Available_Balance => Balance;

    }
}
