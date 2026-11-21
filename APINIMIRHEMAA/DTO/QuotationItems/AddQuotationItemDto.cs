namespace APINIMIRHEMAA.DTO.QuotationItems
{
    // Defines the add quotation item response or transfer shape exchanged through the API.
    public class AddQuotationItemDto
    {
        // Carries requirements for the related API operation. This value is optional.
        public string? Requirements { get; set; }
        // Free-text description that records context or explanations for this add quotation item dto.
        public string? Description { get; set; }
        // Carries width for the related API operation. This value is optional.
        public decimal? Width { get; set; }
        // Carries height for the related API operation. This value is optional.
        public decimal? Height { get; set; }
        // Carries measurement unit for the related API operation. This value is optional.
        public string? Measurement_Unit { get; set; }
        // Financial price per square inch recorded for this add quotation item dto.
        public decimal? Price_Per_Square_Inch { get; set; }
        // Carries quantity for the related API operation. This value is optional.
        public decimal? Quantity { get; set; }
        // Financial unit price recorded for this add quotation item dto.
        public decimal? Unit_Price { get; set; }
        // Financial total amount recorded for this add quotation item dto.
        public decimal? Total_Amount { get; set; }
        // Identifies the related qs record.
        public int QS_ID { get; set; }
        // Carries quotation store ids for the related API operation.
        public List<int> QuotationStoreIds { get; set; } = new();
        // Carries materials for the related API operation.
        public List<AddQuotationItemMaterialDto> Materials { get; set; } = new();
    }

    // Defines the add quotation item material response or transfer shape exchanged through the API.
    public class AddQuotationItemMaterialDto
    {
        // Identifies the related material record.
        public int Material_ID { get; set; }
    }
}
