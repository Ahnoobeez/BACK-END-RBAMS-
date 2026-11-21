namespace APINIMIRHEMAA.DTO
{

    public class RequisitionPreviewDto
    {

        public int Material_ID { get; set; }

        public int? Purchased_ID { get; set; }

        public int Quantity { get; set; }

        public int Unit_Cost { get; set; }

        public string? Supplier { get; set; }

        public string? FromWho { get; set; }

        public string? ToWho { get; set; }
    }
}