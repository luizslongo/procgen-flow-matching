using System.IO;
using System.Net;
using System.Text.Json;
using c2_pcg.flowMatchingDataloader;

namespace c4_api.pcgFlowMatching;

// Handler: POST /pcg-entity-generation/add
// Usage:
//   POST /pcg-entity-generation/add
//   Request:  { "Biome": "overworld", "NumSteps": 20, "NumSamples": 1, "IsRepairApplied": true }
//   Response (200): GenerationHttpOut (id, violation counts, ...)
//   Response (500): { "Type": "ErrorHttpOut", "Message": "<reason>" }
public class GenerationAddHandler
{
    public static void Handle(HttpListenerContext context, GenerationEngine engine, GenerationStoreInterface store)
    {
        string body = ReadRequestBody(context);
        GenerationAddHttpIn input = DeserializeInput(body);

        ErrorReturn validationError = GenerationRequestValidator.ValidateGenerationAddHttpIn(input);
        if (validationError != null)
        {
            HttpResponseWriter.WriteError(context, 500, validationError.Message);
            return;
        }

        if (engine.IsModelAvailable() == false)
        {
            HttpResponseWriter.WriteError(context, 500, "model checkpoint is not available");
            return;
        }

        BiomeTypeEnum biome = BiomeNameMapper.MapBiomeName(input.Biome);
        GenerationHttpOut result = engine.RunGeneration(input, biome);
        store.InsertGeneration(result);
        HttpResponseWriter.WriteJson(context, 200, result);
    }

    static string ReadRequestBody(HttpListenerContext context)
    {
        StreamReader reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        string body = reader.ReadToEnd();
        reader.Close();
        return body;
    }

    // Returns null for empty or malformed json. The JsonException is caught here at
    // the trust boundary so malformed input becomes a clean validation error rather
    // than an unhandled exception that could leak a stack trace to the client.
    static GenerationAddHttpIn DeserializeInput(string body)
    {
        if (body == null || body.Length == 0)
        {
            return null;
        }
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.IncludeFields = true;
        options.PropertyNameCaseInsensitive = true;
        try
        {
            return JsonSerializer.Deserialize<GenerationAddHttpIn>(body, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
