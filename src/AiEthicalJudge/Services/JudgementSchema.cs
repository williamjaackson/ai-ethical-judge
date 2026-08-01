namespace AiEthicalJudge.Services;

/// <summary>
/// The JSON shape a judgement must come back in.
/// </summary>
/// <remarks>
/// Handed to the model as a strict schema, so a reply either matches
/// <see cref="Models.Judgement"/> or the request fails outright — there is no
/// half-parsed middle ground to defend against. Totals are computed by the app
/// rather than asked for, so they are deliberately absent.
/// </remarks>
internal static class JudgementSchema
{
    /// <summary>The name providers use to refer to this schema in errors.</summary>
    public const string Name = "judgement";

    /// <summary>The schema itself.</summary>
    public const string Json = """
        {
          "type": "object",
          "properties": {
            "speech": {
              "type": "array",
              "items": { "$ref": "#/$defs/criterion" }
            },
            "looks": {
              "type": "array",
              "items": { "$ref": "#/$defs/criterion" }
            },
            "summary": {
              "type": "string",
              "description": "A short overall verdict on the scenario as it stands."
            }
          },
          "required": ["speech", "looks", "summary"],
          "additionalProperties": false,
          "$defs": {
            "criterion": {
              "type": "object",
              "properties": {
                "criterion": {
                  "type": "string",
                  "description": "The user-defined criterion being scored, copied verbatim."
                },
                "score": {
                  "type": "integer",
                  "enum": [1, 2, 3, 4, 5],
                  "description": "The mark for this criterion, from 1 to 5."
                },
                "comment": {
                  "type": "string",
                  "description": "A sentence or two justifying the mark."
                }
              },
              "required": ["criterion", "score", "comment"],
              "additionalProperties": false
            }
          }
        }
        """;
}
