using System;
using WebApolice.BitrixIntegration.Modules.Bitrix;
using Xunit;

namespace WebApolice.BitrixIntegration.Tests;

public class BitrixUrlBuilderTests
{
    [Fact]
    public void BuildUrl_WithoutTrailingSlash_ShouldAddSlash()
    {
        var baseUrl = "https://bitrix24.local/rest/1/abcxyz";
        var method = "crm.contact.add.json";

        var url = BitrixUrlBuilder.BuildUrl(baseUrl, method);

        Assert.Equal("https://bitrix24.local/rest/1/abcxyz/crm.contact.add.json", url);
    }

    [Fact]
    public void BuildUrl_WithTrailingSlash_ShouldNotAddAnotherSlash()
    {
        var baseUrl = "https://bitrix24.local/rest/1/abcxyz/";
        var method = "crm.contact.add.json";

        var url = BitrixUrlBuilder.BuildUrl(baseUrl, method);

        Assert.Equal("https://bitrix24.local/rest/1/abcxyz/crm.contact.add.json", url);
    }

    [Fact]
    public void MaskWebhookUrl_ShouldMaskTokenProperly()
    {
        var url = "https://bitrix24.local/rest/1/abc123def456xyz/crm.contact.add.json";
        
        var maskedUrl = BitrixUrlBuilder.MaskWebhookUrl(url);

        Assert.Equal("https://bitrix24.local/rest/1/abc***xyz/crm.contact.add.json", maskedUrl);
    }
}
