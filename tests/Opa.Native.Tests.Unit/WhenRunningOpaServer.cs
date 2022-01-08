using NUnit.Framework;
using Opa.Native;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Opa.Native.Tests.Unit;

public class WhenRunningOpaServer
{
    [Test]
    public async Task Should_get_policies()
    {
        var opa = new Opa();
        await using var handle = await opa.StartServerAsync();
        using var httpClient = new HttpClient{BaseAddress = new Uri("http://127.0.0.1:8181")};
        var response = await httpClient.GetAsync("/v1/policies");
        response.EnsureSuccessStatusCode();
        Assert.AreEqual("{\"result\":[]}", await response.Content.ReadAsStringAsync());
    }
}
