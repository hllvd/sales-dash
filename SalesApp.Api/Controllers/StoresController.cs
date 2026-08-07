using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.Attributes;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StoresController : ControllerBase
    {
        private readonly IStoreRepository _storeRepository;

        public StoresController(IStoreRepository storeRepository)
        {
            _storeRepository = storeRepository;
        }

        // GET: api/stores/all (Available to all authenticated users for dropdown selection)
        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<StoreResponse>>>> GetActiveStores()
        {
            var stores = (await _storeRepository.GetActiveAsync()).ToList();
            var responses = stores.Select(MapToStoreResponse).ToList();

            return Ok(new ApiResponse<List<StoreResponse>>
            {
                Success = true,
                Data = responses,
                Message = "Active stores retrieved successfully"
            });
        }

        // GET: api/stores (Superadmin only)
        [HttpGet]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<List<StoreResponse>>>> GetAllStores()
        {
            var stores = (await _storeRepository.GetAllAsync()).ToList();
            var responses = stores.Select(MapToStoreResponse).ToList();

            return Ok(new ApiResponse<List<StoreResponse>>
            {
                Success = true,
                Data = responses,
                Message = "Stores retrieved successfully"
            });
        }

        // GET: api/stores/{id}
        [HttpGet("{id}")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<StoreResponse>>> GetStore(int id)
        {
            var store = await _storeRepository.GetByIdAsync(id);
            if (store == null)
            {
                return NotFound(new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = "Loja não encontrada"
                });
            }

            return Ok(new ApiResponse<StoreResponse>
            {
                Success = true,
                Data = MapToStoreResponse(store),
                Message = "Loja obtida com sucesso"
            });
        }

        // POST: api/stores
        [HttpPost]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<StoreResponse>>> CreateStore(CreateStoreRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = "O nome da loja é obrigatório"
                });
            }

            if (string.IsNullOrWhiteSpace(request.State) || request.State.Trim().Length != 2)
            {
                return BadRequest(new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = "O estado deve ter exatamente 2 caracteres"
                });
            }

            if (await _storeRepository.NameExistsAsync(request.Name))
            {
                return BadRequest(new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = "Já existe uma loja cadastrada com este nome"
                });
            }

            var store = new Store
            {
                Name = request.Name.Trim(),
                State = request.State.Trim().ToUpperInvariant(),
                IsActive = true
            };

            var created = await _storeRepository.CreateAsync(store);

            return CreatedAtAction(
                nameof(GetStore),
                new { id = created.Id },
                new ApiResponse<StoreResponse>
                {
                    Success = true,
                    Data = MapToStoreResponse(created),
                    Message = "Loja criada com sucesso"
                });
        }

        // PUT: api/stores/{id}
        [HttpPut("{id}")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<StoreResponse>>> UpdateStore(int id, UpdateStoreRequest request)
        {
            var store = await _storeRepository.GetByIdAsync(id);
            if (store == null)
            {
                return NotFound(new ApiResponse<StoreResponse>
                {
                    Success = false,
                    Message = "Loja não encontrada"
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (await _storeRepository.NameExistsAsync(request.Name, id))
                {
                    return BadRequest(new ApiResponse<StoreResponse>
                    {
                        Success = false,
                        Message = "Já existe uma loja cadastrada com este nome"
                    });
                }
                store.Name = request.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.State))
            {
                if (request.State.Trim().Length != 2)
                {
                    return BadRequest(new ApiResponse<StoreResponse>
                    {
                        Success = false,
                        Message = "O estado deve ter exatamente 2 caracteres"
                    });
                }
                store.State = request.State.Trim().ToUpperInvariant();
            }

            if (request.IsActive.HasValue)
            {
                store.IsActive = request.IsActive.Value;
            }

            var updated = await _storeRepository.UpdateAsync(store);

            return Ok(new ApiResponse<StoreResponse>
            {
                Success = true,
                Data = MapToStoreResponse(updated),
                Message = "Loja atualizada com sucesso"
            });
        }

        // DELETE: api/stores/{id}
        [HttpDelete("{id}")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteStore(int id)
        {
            var store = await _storeRepository.GetByIdAsync(id);
            if (store == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Loja não encontrada"
                });
            }

            await _storeRepository.DeleteAsync(id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Loja excluída com sucesso"
            });
        }

        private StoreResponse MapToStoreResponse(Store store)
        {
            return new StoreResponse
            {
                Id = store.Id,
                Name = store.Name,
                State = store.State,
                IsActive = store.IsActive,
                CreatedAt = store.CreatedAt,
                UpdatedAt = store.UpdatedAt
            };
        }
    }
}
