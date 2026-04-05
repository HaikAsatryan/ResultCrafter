namespace ResultCrafter.Core.Primitives;

/// <summary>
///    Indicates which HTTP success status code a <see cref="Result{T}" /> should map to.
///    Set automatically by the <see cref="Result{T}" /> factory methods; not intended to be
///    assigned directly by application code.
/// </summary>
public enum SuccessKind : byte
{
   /// <summary>Maps to HTTP 200 OK. This is the CLR default value.</summary>
   Ok = 0,

   /// <summary>
   ///    Maps to HTTP 201 Created. A <see cref="Result{T}.Location" /> URI must be provided.
   /// </summary>
   Created = 1,

   /// <summary>
   ///    Maps to HTTP 202 Accepted. Signals that the request has been queued or handed
   ///    off to a background process. Optionally carries a <see cref="Result{T}.Location" />
   ///    poll URL.
   /// </summary>
   Accepted = 2,

   /// <summary>
   ///    Maps to HTTP 204 No Content. Used by the void <see cref="Result" /> to indicate
   ///    a successful operation that returns no body.
   /// </summary>
   NoContent = 3
}