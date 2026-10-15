using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Protection;

/// <summary>Validates the native encrypted ring before framework logging or damaged-key skipping can occur.</summary>
internal sealed class EncryptedKeyRepository(string directory) : IXmlRepository
{
    private const int MaximumFiles = 256;
    private const string XmlPattern = "*.xml";
    private const string KeyElement = "key";
    private const string RevocationElement = "revocation";
    private const string PlaintextKeyElement = "masterKey";
    private const string VersionAttribute = "version";
    private const string SupportedVersion = "1";
    private const string IdAttribute = "id";
    private const string CreationElement = "creationDate";
    private const string ActivationElement = "activationDate";
    private const string ExpirationElement = "expirationDate";
    private const string DescriptorElement = "descriptor";
    private const string DeserializerAttribute = "deserializerType";
    private const string EncryptedSecretElement = "encryptedSecret";
    private const string ProtectionNamespace = "http://schemas.asp.net/2015/03/dataProtection";
    private const string EncryptionElement = "encryption";
    private const string ValidationElement = "validation";
    private const string AlgorithmAttribute = "algorithm";
    private const string EncryptionAlgorithm = "AES_256_CBC";
    private const string ValidationAlgorithm = "HMACSHA256";
    private const string DecryptorAttribute = "decryptorType";
    private const string EncryptedDataElement = "EncryptedData";
    private const string EncryptionNamespace = "http://www.w3.org/2001/04/xmlenc#";
    private const string RevocationDateElement = "revocationDate";
    private const string ReasonElement = "reason";
    private const string AllKeys = "*";

    private readonly FileSystemXmlRepository writer = new(new DirectoryInfo(directory), NullLoggerFactory.Instance);

    /// <summary>Validates every bounded native element before the framework can log or skip corrupt material.</summary>
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        PrivateStorage.RequireDirectory(directory);
        var files = Directory.EnumerateFiles(directory, XmlPattern).Take(MaximumFiles + 1).ToArray();
        if (files.Length > MaximumFiles)
        {
            throw new ProtectionMaterialException();
        }

        var elements = new List<XElement>();
        var ids = new HashSet<Guid>();
        foreach (var file in files)
        {
            PrivateStorage.RequireRingFile(file);
            using var reader = XmlReader.Create(file, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = PrivateStorage.MaximumFileBytes
            });
            var element = XElement.Load(reader);
            Validate(element);
            if (element.Name == KeyElement && !ids.Add(Guid.Parse(element.Attribute(IdAttribute)!.Value)))
            {
                throw new ProtectionMaterialException();
            }

            elements.Add(element);
        }
        return elements;
    }

    /// <summary>Validates native encrypted format before persisting through the framework repository.</summary>
    public void StoreElement(XElement element, string friendlyName)
    {
        PrivateStorage.RequireDirectory(directory);
        Validate(element);
        writer.StoreElement(element, friendlyName);
    }

    /// <summary>Rejects unsupported or plaintext elements and dispatches strict native shape validation.</summary>
    private static void Validate(XElement element)
    {
        if (element.Name != KeyElement && element.Name != RevocationElement)
        {
            throw new ProtectionMaterialException();
        }

        if (element.Descendants().Any(node => node.Name.LocalName == PlaintextKeyElement))
        {
            throw new ProtectionMaterialException();
        }

        if (element.Attribute(VersionAttribute)?.Value != SupportedVersion)
        {
            throw new ProtectionMaterialException();
        }

        if (element.Name == KeyElement)
        {
            ValidateKey(element);
        }
        else
        {
            ValidateRevocation(element);
        }
    }

    /// <summary>Checks native key identity, timestamps and the single supported encrypted descriptor.</summary>
    private static void ValidateKey(XElement element)
    {
        if (!Guid.TryParse(element.Attribute(IdAttribute)?.Value, out _))
        {
            throw new ProtectionMaterialException();
        }
        RequireChildren(element, CreationElement, ActivationElement, ExpirationElement, DescriptorElement);
        foreach (var field in new[] { CreationElement, ActivationElement, ExpirationElement })
        {
            _ = XmlConvert.ToDateTimeOffset(element.Element(field)!.Value);
        }
        var outer = element.Element(DescriptorElement)!;
        if (outer.Attribute(DeserializerAttribute)?.Value != typeof(AuthenticatedEncryptorDescriptorDeserializer).AssemblyQualifiedName)
        {
            throw new ProtectionMaterialException();
        }
        RequireChildren(outer, DescriptorElement);
        ValidateDescriptor(outer.Element(DescriptorElement)!);
    }

    /// <summary>Rejects plaintext or unexpected native encryption algorithms and XML decryptor types.</summary>
    private static void ValidateDescriptor(XElement inner)
    {
        XName secretName = XName.Get(EncryptedSecretElement, ProtectionNamespace);
        RequireChildren(inner, EncryptionElement, ValidationElement, secretName);
        if (inner.Element(EncryptionElement)!.Attribute(AlgorithmAttribute)?.Value != EncryptionAlgorithm ||
            inner.Element(ValidationElement)!.Attribute(AlgorithmAttribute)?.Value != ValidationAlgorithm)
        {
            throw new ProtectionMaterialException();
        }
        var encrypted = inner.Element(secretName)!;
        if (encrypted.Attribute(DecryptorAttribute)?.Value != typeof(EncryptedXmlDecryptor).AssemblyQualifiedName)
        {
            throw new ProtectionMaterialException();
        }
        RequireChildren(encrypted, XName.Get(EncryptedDataElement, EncryptionNamespace));
    }

    /// <summary>Validates native per-key or all-key revocation without silently dropping malformed denial records.</summary>
    private static void ValidateRevocation(XElement element)
    {
        RequireChildren(element, RevocationDateElement, KeyElement, ReasonElement);
        var keyId = element.Element(KeyElement)!.Attribute(IdAttribute)?.Value;
        if (keyId != AllKeys && !Guid.TryParse(keyId, out _))
        {
            throw new ProtectionMaterialException();
        }
        _ = XmlConvert.ToDateTimeOffset(element.Element(RevocationDateElement)!.Value);
    }

    /// <summary>Requires each expected native child exactly once and rejects unknown children.</summary>
    private static void RequireChildren(XElement element, params XName[] expected)
    {
        var names = element.Elements().Select(child => child.Name).ToArray();
        if (names.Length != expected.Length || expected.Any(name => names.Count(candidate => candidate == name) != 1))
        {
            throw new ProtectionMaterialException();
        }
    }
}
