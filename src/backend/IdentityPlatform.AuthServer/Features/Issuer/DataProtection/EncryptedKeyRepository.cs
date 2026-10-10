using System.Xml;
using System.Xml.Linq;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Issuer.DataProtection;

// Native framework format/storage; validate before the framework can log or skip damaged material.
internal sealed class EncryptedKeyRepository(string directory) : IXmlRepository
{
    private readonly FileSystemXmlRepository writer = new(new DirectoryInfo(directory), NullLoggerFactory.Instance);

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        PrivateStorage.RequireDirectory(directory);
        var files = Directory.EnumerateFiles(directory, "*.xml").Take(257).ToArray();
        if (files.Length > 256)
        {
            throw new IssuerProtectionException();
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
            if (element.Name == "key" && !ids.Add(Guid.Parse(element.Attribute("id")!.Value)))
            {
                throw new IssuerProtectionException();
            }

            elements.Add(element);
        }
        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        PrivateStorage.RequireDirectory(directory);
        Validate(element);
        writer.StoreElement(element, friendlyName);
    }

    private static void Validate(XElement element)
    {
        if (element.Name != "key" && element.Name != "revocation")
        {
            throw new IssuerProtectionException();
        }

        if (element.Descendants().Any(node => node.Name.LocalName == "masterKey"))
        {
            throw new IssuerProtectionException();
        }

        if (element.Attribute("version")?.Value != "1")
        {
            throw new IssuerProtectionException();
        }

        if (element.Name == "key")
        {
            if (!Guid.TryParse(element.Attribute("id")?.Value, out _))
            {
                throw new IssuerProtectionException();
            }

            RequireChildren(element, "creationDate", "activationDate", "expirationDate", "descriptor");
            foreach (var field in new[] { "creationDate", "activationDate", "expirationDate" })
            {
                _ = XmlConvert.ToDateTimeOffset(element.Element(field)!.Value);
            }

            var outer = element.Element("descriptor")!;
            if (outer.Attribute("deserializerType")?.Value != typeof(AuthenticatedEncryptorDescriptorDeserializer).AssemblyQualifiedName)
            {
                throw new IssuerProtectionException();
            }

            RequireChildren(outer, "descriptor");
            var inner = outer.Element("descriptor")!;
            XName secretName = XName.Get("encryptedSecret", "http://schemas.asp.net/2015/03/dataProtection");
            RequireChildren(inner, "encryption", "validation", secretName);
            if (inner.Element("encryption")!.Attribute("algorithm")?.Value != "AES_256_CBC" ||
                inner.Element("validation")!.Attribute("algorithm")?.Value != "HMACSHA256")
            {
                throw new IssuerProtectionException();
            }

            var encrypted = inner.Element(secretName)!;
            if (encrypted.Attribute("decryptorType")?.Value != typeof(EncryptedXmlDecryptor).AssemblyQualifiedName)
            {
                throw new IssuerProtectionException();
            }

            RequireChildren(encrypted, XName.Get("EncryptedData", "http://www.w3.org/2001/04/xmlenc#"));
        }
        else
        {
            RequireChildren(element, "revocationDate", "key", "reason");
            var keyId = element.Element("key")!.Attribute("id")?.Value;
            if (keyId != "*" && !Guid.TryParse(keyId, out _))
            {
                throw new IssuerProtectionException();
            }

            _ = XmlConvert.ToDateTimeOffset(element.Element("revocationDate")!.Value);
        }
    }

    private static void RequireChildren(XElement element, params XName[] expected)
    {
        var names = element.Elements().Select(child => child.Name).ToArray();
        if (names.Length != expected.Length || expected.Any(name => names.Count(candidate => candidate == name) != 1))
        {
            throw new IssuerProtectionException();
        }
    }
}
