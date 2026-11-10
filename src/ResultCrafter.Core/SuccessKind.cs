namespace ResultCrafter;

/// <summary>
///     Indicates which HTTP success status code a <see cref="Result{T}" /> or <see cref="Result" /> maps to.
///     Set by the factory methods; not intended to be assigned by application code.
/// </summary>
public enum SuccessKind : byte
{
    /// <summary>
    ///     The CLR default value. Not a success: failures carry it, and so does a <c>default</c> result,
    ///     which no factory produces and which is therefore invalid.
    /// </summary>
    None = 0,

    /// <summary>Maps to HTTP 200 OK.</summary>
    Ok = 1,

    /// <summary>
    ///     Maps to HTTP 201 Created. A <see cref="Result{T}.Location" /> URI must be provided.
    /// </summary>
    Created = 2,

    /// <summary>
    ///     Maps to HTTP 202 Accepted. Signals that the request has been queued or handed
    ///     off to a background process. Optionally carries a <see cref="Result{T}.Location" />
    ///     poll URL.
    /// </summary>
    Accepted = 3,

    /// <summary>
    ///     Maps to HTTP 204 No Content. Used by the void <see cref="Result" /> to indicate
    ///     a successful operation that returns no body.
    /// </summary>
    NoContent = 4
}
