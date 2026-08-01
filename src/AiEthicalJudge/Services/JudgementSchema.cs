namespace AiEthicalJudge.Services;

/// <summary>
/// The JSON shape a judgement must come back in.
/// </summary>
/// <remarks>
/// Handed to the model as a strict schema, so a reply either matches
/// <see cref="Models.Judgement"/> or the request fails outright — there is no
/// half-parsed middle ground to defend against. <c>total</c> is computed here
/// rather than asked for, so it is deliberately absent.
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
            "theme": { "$ref": "#/$defs/criterion" },
            "creativity": { "$ref": "#/$defs/criterion" },
            "execution": { "$ref": "#/$defs/criterion" },
            "summary": {
              "type": "string",
              "description": "A short overall verdict on the scenario as it stands."
            }
          },
          "required": ["theme", "creativity", "execution", "summary"],
          "additionalProperties": false,
          "$defs": {
            "criterion": {
              "type": "object",
              "properties": {
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
              "required": ["score", "comment"],
              "additionalProperties": false
            }
          }
        }
        """;
}
