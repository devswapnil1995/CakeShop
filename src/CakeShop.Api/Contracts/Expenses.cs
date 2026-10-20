using System.ComponentModel.DataAnnotations;

namespace CakeShop.Api.Contracts
{

    public sealed record CreateExpenseRequest(
        [property: Required, StringLength(80, MinimumLength = 1)]
    string Category,

        [property: Range(typeof(decimal), "0.01", "9999999999999999")]
    decimal Amount,

        DateOnly ExpenseDate,

        [property: StringLength(500)]
    string? Description);

    public sealed record UpdateExpenseRequest(
        [property: Required, StringLength(80, MinimumLength = 1)]
    string Category,

        [property: Range(typeof(decimal), "0.01", "9999999999999999")]
    decimal Amount,

        DateOnly ExpenseDate,

        [property: StringLength(500)]
    string? Description);
}
