using System.ComponentModel.DataAnnotations;

namespace MunicipalElections.Dtos;

public record RegisterRequest(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, StringLength(100, MinimumLength = 6)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record UserDto(string Id, string? Name, string? Email);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);
