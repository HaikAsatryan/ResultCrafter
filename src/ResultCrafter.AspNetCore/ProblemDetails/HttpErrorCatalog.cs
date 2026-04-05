using ResultCrafter.Core.Primitives;

namespace ResultCrafter.AspNetCore.ProblemDetails;

/// <summary>
///    Maps <see cref="ErrorType" /> values to HTTP status codes, ProblemDetails titles,
///    and default detail messages.
/// </summary>
public static class HttpErrorCatalog
{
   /// <summary>Maps <paramref name="type" /> to its RFC 9457 HTTP status code.</summary>
   public static int Status(ErrorType type)
   {
      return type switch
      {
         ErrorType.BadRequest => 400,
         ErrorType.Unauthorized => 401,
         ErrorType.Forbidden => 403,
         ErrorType.NotFound => 404,
         ErrorType.Conflict => 409,
         ErrorType.ConcurrencyConflict => 409,
         _ => throw new ArgumentOutOfRangeException(nameof(type),
            type,
            $"ErrorType '{type}' has no HTTP status mapping.")
      };
   }

   /// <summary>Maps <paramref name="type" /> to its snake_case ProblemDetails <c>title</c> string.</summary>
   public static string Title(ErrorType type)
   {
      return type switch
      {
         ErrorType.BadRequest => "bad_request",
         ErrorType.Unauthorized => "unauthorized",
         ErrorType.Forbidden => "forbidden",
         ErrorType.NotFound => "not_found",
         ErrorType.Conflict => "conflict",
         ErrorType.ConcurrencyConflict => "concurrency_conflict",
         _ => throw new ArgumentOutOfRangeException(nameof(type),
            type,
            $"ErrorType '{type}' has no title mapping.")
      };
   }

   /// <summary>Returns the default <c>detail</c> message for the given <paramref name="type" />.</summary>
   public static string DefaultDetail(ErrorType type)
   {
      return type switch
      {
         ErrorType.BadRequest => "the_request_was_invalid_or_cannot_be_otherwise_served",
         ErrorType.NotFound => "resource_not_found",
         ErrorType.Conflict => "conflict",
         ErrorType.ConcurrencyConflict => "concurrency_conflict",
         ErrorType.Unauthorized => "unauthorized",
         ErrorType.Forbidden => "forbidden",
         _ => throw new ArgumentOutOfRangeException(nameof(type),
            type,
            $"ErrorType '{type}' has no default detail.")
      };
   }

   /// <summary>Returns the RFC 9110 <c>type</c> URI for the given <paramref name="type" />.</summary>
   public static string TypeUri(ErrorType type)
   {
      return type switch
      {
         ErrorType.BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
         ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
         ErrorType.Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
         ErrorType.NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
         ErrorType.Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
         ErrorType.ConcurrencyConflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
         _ => throw new ArgumentOutOfRangeException(nameof(type),
            type,
            $"ErrorType '{type}' has no type URI mapping.")
      };
   }

   /// <summary>Returns the error's own detail message, falling back to the catalog default.</summary>
   public static string ResolveDetail(Error error)
   {
      return error.Detail ?? DefaultDetail(error.Type);
   }
}