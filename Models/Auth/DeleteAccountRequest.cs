using System.ComponentModel.DataAnnotations;

public class DeleteAccountRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }
}