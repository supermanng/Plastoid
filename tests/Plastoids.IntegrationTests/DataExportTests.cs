using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DataExportTests(PlastoidsApiFactory factory)
{
    [Fact]
    public async Task Data_export_is_fulfilled_by_the_job_with_json_and_pdf_in_blob_storage()
    {
        var phone = NewPhone();
        var (client, _) = await factory.SignInWithPhoneAsync(phone, "Chiamaka");
        await client.PutAsJsonAsync("/api/v1/identity/me/goals", new { careerGoal = "Data analyst", interests = new[] { "sql" } });

        var requested = await client.PostAsync("/api/v1/identity/me/data-export", null);
        requested.StatusCode.Should().Be(HttpStatusCode.Accepted, await requested.Content.ReadAsStringAsync());
        var exportId = (await requested.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("exportId").GetGuid();

        // A second request while one is pending returns the same id.
        var again = await client.PostAsync("/api/v1/identity/me/data-export", null);
        (await again.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("exportId").GetGuid().Should().Be(exportId);

        JsonElement status = default;
        await WaitUntilAsync(async () =>
        {
            status = await client.GetFromJsonAsync<JsonElement>($"/api/v1/identity/me/data-export/{exportId}", Json);
            return status.GetProperty("status").GetString() == "completed";
        }, TimeSpan.FromSeconds(60), "the data export job");

        var jsonUrl = status.GetProperty("jsonUrl").GetString()!;
        var pdfUrl = status.GetProperty("pdfUrl").GetString()!;
        using var http = new HttpClient();
        var json = await http.GetStringAsync(jsonUrl);
        json.Should().Contain("Chiamaka").And.Contain("Data analyst");
        using (var document = JsonDocument.Parse(json))
        {
            document.RootElement.GetProperty("account").GetProperty("phone").GetString().Should().Be(phone);
        }
        var pdf = await http.GetByteArrayAsync(pdfUrl);
        System.Text.Encoding.Latin1.GetString(pdf, 0, 8).Should().StartWith("%PDF-1.4");
        System.Text.Encoding.Latin1.GetString(pdf).Should().Contain("Chiamaka");

        // Another user cannot read it.
        var (other, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Other");
        (await other.GetAsync($"/api/v1/identity/me/data-export/{exportId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
