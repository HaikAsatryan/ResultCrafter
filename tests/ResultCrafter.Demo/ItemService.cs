using System.Collections.Concurrent;
using FluentValidation;
using ResultCrafter.Core.Primitives;
using ResultCrafter.FluentValidation;

namespace ResultCrafter.Demo;

/// <summary>
///    In-memory service that exercises every ResultCrafter path:
///    Success kinds  :  Ok, Created, Accepted (typed), Accepted (void), NoContent
///    Error types    :  BadRequest (plain + field errors via FluentValidation), NotFound,
///    Conflict, ConcurrencyConflict, Unauthorized, Forbidden
///    Exception path :  unhandled throw -> 500 via IExceptionHandler
/// </summary>
public sealed class ItemService(IValidator<CreateItemRequest> createValidator)
{
   // Tracks items currently being processed async (simulates a background queue).
   private readonly ConcurrentDictionary<int, byte> _processingQueue = new();
   // ── Storage ───────────────────────────────────────────────────────────────

   private readonly ConcurrentDictionary<int, ItemEntity> _store = new();
   private int _nextId;

   // ── Queries ───────────────────────────────────────────────────────────────

   /// <summary>GET /items/{id} -> Ok or NotFound</summary>
   public async Task<Result<ItemDto>> GetAsync(int id, CancellationToken ct)
   {
      await Task.CompletedTask; // simulates a real async DB call

      if (!_store.TryGetValue(id, out var entity))
      {
         return Error.NotFound($"Item {id} does not exist.");
      }

      return entity.ToDto();
   }

   /// <summary>
   ///    GET /items/admin -> Ok or Unauthorized or Forbidden
   ///    Unauthorized  : no API key header provided at all.
   ///    Forbidden     : key present but the role is not "admin".
   /// </summary>
   public async Task<Result<List<ItemDto>>> GetAllAdminAsync(string? apiKey,
      string? role,
      CancellationToken ct)
   {
      await Task.CompletedTask; // simulates a real async DB call

      if (string.IsNullOrWhiteSpace(apiKey))
      {
         return Error.Unauthorized("A valid API key is required.");
      }

      if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
      {
         return Error.Forbidden("Only admins can list all items.");
      }

      var all = _store.Values
                      .Select(e => e.ToDto())
                      .ToList();

      return all;
   }

   // ── Commands ──────────────────────────────────────────────────────────────

   /// <summary>
   ///    POST /items -> Created or BadRequest (field errors via FluentValidation)
   /// </summary>
   public async Task<Result<ItemDto>> CreateAsync(CreateItemRequest req, CancellationToken ct)
   {
      var error = await createValidator.ValidateToResultAsync(req, ct);
      if (error is not null)
      {
         return Result<ItemDto>.Fail(error.Value);
      }

      var id = Interlocked.Increment(ref _nextId);
      var entity = new ItemEntity(id, req.Name!, req.Price, req.Stock, 1);
      _store[id] = entity;

      return Result<ItemDto>.Created($"/api/items/{id}", entity.ToDto());
   }

   /// <summary>
   ///    PUT /items/{id} -> Ok or NotFound, BadRequest (field errors), Conflict, ConcurrencyConflict
   ///    Conflict            : another item already has the same name.
   ///    ConcurrencyConflict : the request's version token is stale.
   /// </summary>
   public async Task<Result<ItemDto>> UpdateAsync(int id, UpdateItemRequest req, CancellationToken ct)
   {
      await Task.CompletedTask; // simulates a real async DB call

      if (!_store.TryGetValue(id, out var entity))
      {
         return Error.NotFound($"Item {id} does not exist.");
      }

      if (entity.Version != req.Version)
      {
         return Error.ConcurrencyConflict(
            $"Item {id} was modified by another request. Fetch the latest version and retry.");
      }

      var errors = ValidateUpdate(req);
      if (errors.Count > 0)
      {
         return Error.BadRequest(errors);
      }

      var nameTaken = _store.Values.Any(e =>
         e.Id != id && string.Equals(e.Name, req.Name, StringComparison.OrdinalIgnoreCase));

      if (nameTaken)
      {
         return Error.Conflict($"An item named '{req.Name}' already exists.");
      }

      var updated = entity with
      {
         Name = req.Name!,
         Price = req.Price,
         Stock = req.Stock,
         Version = entity.Version + 1
      };

      _store[id] = updated;
      return updated.ToDto();
   }

   /// <summary>DELETE /items/{id} -> NoContent or NotFound</summary>
   public async Task<Result> DeleteAsync(int id, CancellationToken ct)
   {
      await Task.CompletedTask; // simulates a real async DB call

      return !_store.TryRemove(id, out _)
         ? Result.Fail(Error.NotFound($"Item {id} does not exist."))
         : Result.NoContent();
   }

   /// <summary>
   ///    POST /items/{id}/reserve -> Accepted(ItemDto) or NotFound or Forbidden
   ///    Returns Accepted (202 with body) because reservation triggers an async
   ///    background workflow : the item is not fully reserved yet.
   ///    Forbidden : item is already reserved by someone else.
   /// </summary>
   public async Task<Result<ItemDto>> ReserveAsync(int id, CancellationToken ct)
   {
      await Task.CompletedTask; // simulates a real async DB call

      if (!_store.TryGetValue(id, out var entity))
      {
         return Error.NotFound($"Item {id} does not exist.");
      }

      if (entity.IsReserved)
      {
         return Error.Forbidden($"Item {id} is already reserved and cannot be taken.");
      }

      var reserved = entity with
      {
         IsReserved = true
      };
      _store[id] = reserved;

      // 202: caller should poll GET /items/{id} to confirm completion.
      return Result<ItemDto>.Accepted(reserved.ToDto(), $"/api/items/{id}");
   }

   /// <summary>
   ///    POST /items/bulk-delete -> Accepted (void) or BadRequest
   ///    BadRequest with a plain detail string : empty or null ID list.
   ///    BadRequest with a structured errors dict : list contains invalid entries.
   ///    Returns Accepted (void, 202 no body) because deletion is queued asynchronously.
   /// </summary>
   public async Task<Result> BulkDeleteAsync(BulkDeleteRequest req, CancellationToken ct)
   {
      await Task.CompletedTask; // simulates a real async DB call

      if (req.Ids is null || req.Ids.Count == 0)
      {
         return Error.BadRequest("At least one item ID must be provided.");
      }

      var fieldErrors = new Dictionary<string, string[]>();

      var nonPositive = req.Ids
                           .Where(x => x <= 0)
                           .ToArray();
      if (nonPositive.Length > 0)
      {
         fieldErrors["ids"] = [$"IDs must be positive integers. Invalid: {string.Join(", ", nonPositive)}."];
      }

      if (req.Ids.Count > 100)
      {
         fieldErrors["ids"] =
         [
            ..fieldErrors.GetValueOrDefault("ids", []),
            "Cannot delete more than 100 items in a single request."
         ];
      }

      if (fieldErrors.Count > 0)
      {
         return Error.BadRequest(fieldErrors);
      }

      foreach (var id in req.Ids)
      {
         _processingQueue.TryAdd(id, 0);
      }

      // 202: caller should check back later; no Location because it's a queue.
      return Result.Accepted();
   }

   // ── Validation helpers ────────────────────────────────────────────────────

   private static Dictionary<string, string[]> ValidateUpdate(UpdateItemRequest req)
   {
      var errors = new Dictionary<string, string[]>();

      if (string.IsNullOrWhiteSpace(req.Name))
      {
         errors["name"] = ["Name is required."];
      }
      else if (req.Name.Length > 100)
      {
         errors["name"] = ["Name must be 100 characters or fewer."];
      }

      if (req.Price <= 0)
      {
         errors["price"] = ["Price must be greater than 0."];
      }

      if (req.Stock < 0)
      {
         errors["stock"] = ["Stock cannot be negative."];
      }

      return errors;
   }

   // ── Internal entity ───────────────────────────────────────────────────────

   private sealed record ItemEntity(int Id, string Name, decimal Price, int Stock, int Version, bool IsReserved = false)
   {
      public ItemDto ToDto()
      {
         return new ItemDto(Id, Name, Price, Stock, Version, IsReserved);
      }
   }
}
