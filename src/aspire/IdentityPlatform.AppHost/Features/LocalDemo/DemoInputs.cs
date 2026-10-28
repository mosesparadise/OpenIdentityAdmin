using System.Security.Cryptography;
using System.Text.Json;

namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Validates fresh dashboard values before producing the unchanged native bootstrap document.</summary>
internal sealed class DemoInputs : IDisposable
{
    private const string UsernameLabel = "Synthetic administrator username";
    private const string PasswordLabel = "Password";
    private const string OperatorReference = "local-demo";

    private const string Username = "username";
    private const string Password = "password";
    private const int UsernameLimit = 128;
    private const int PasswordMinimum = 16;
    private const int PasswordLimit = 256;
    private const int ByteLimit = 4096;
    private const string Failure = "Enter a valid synthetic username and a password of 16–256 characters.";
    internal IReadOnlyList<InteractionInput> Fields
    {
        get;
    } =
    [
        new() { Name = Username, Label = UsernameLabel, InputType = InputType.Text, Required = true, MaxLength = UsernameLimit },
        new() { Name = Password, Label = PasswordLabel, InputType = InputType.SecretText, Required = true, MaxLength = PasswordLimit }
    ];
    private byte[] bytes = [];
    private InteractionInputCollection? returned;

    /// <summary>Revalidates returned input server-side instead of trusting dialog length or required metadata.</summary>
    internal ReadOnlyMemory<byte> Encode(InteractionInputCollection values)
    {
        returned = values;
        var username = values[Username].Value;
        var password = values[Password].Value;
        if (username is null || username.Length is < 1 or > UsernameLimit ||
            !username.All(value => char.IsAsciiLetterOrDigit(value) || value is '.' or '_' or '@' or '-') ||
            password is null || password.Length is < PasswordMinimum or > PasswordLimit)
        {
            throw new InvalidOperationException(Failure);
        }
        bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            operationId = Guid.NewGuid(),
            operatorReference = OperatorReference,
            userName = username,
            password
        });
        if (bytes.Length > ByteLimit)
        {
            throw new InvalidOperationException(Failure);
        }
        return bytes;
    }

    /// <summary>Clears mutable buffers and dialog references without claiming erasure of managed password strings.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(bytes);
        foreach (var field in Fields)
        {
            field.Value = null;
        }
        if (returned is not null)
        {
            foreach (var field in returned)
            {
                field.Value = null;
            }
        }
    }
}
