using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/point-of-sale")]
    public class PointOfSaleController : ControllerBase
    {
        private readonly IPVRepository _pvRepository;
        
        public PointOfSaleController(IPVRepository pvRepository)
        {
            _pvRepository = pvRepository;
        }
        
        [HttpGet]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<List<PVResponse>>>> GetAll()
        {
            var pvs = await _pvRepository.GetAllAsync();
            var response = pvs.Select(p => new PVResponse
            {
                Id = p.Id,
                Name = p.Name,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();
            
            return Ok(new ApiResponse<List<PVResponse>>
            {
                Success = true,
                Data = response,
                Message = "PVs retrieved successfully"
            });
        }
        
        [HttpGet("{id}")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<PVResponse>>> GetById(int id)
        {
            var pv = await _pvRepository.GetByIdAsync(id);
            
            if (pv == null)
            {
                return NotFound(new ApiResponse<PVResponse>
                {
                    Success = false,
                    Message = "PV not found"
                });
            }
            
            var response = new PVResponse
            {
                Id = pv.Id,
                Name = pv.Name,
                CreatedAt = pv.CreatedAt,
                UpdatedAt = pv.UpdatedAt
            };
            
            return Ok(new ApiResponse<PVResponse>
            {
                Success = true,
                Data = response,
                Message = "PV retrieved successfully"
            });
        }
        
        [HttpPost]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<PVResponse>>> Create(PVRequest request)
        {
            // Check if ID already exists
            if (await _pvRepository.ExistsAsync(request.Id))
            {
                return BadRequest(new ApiResponse<PVResponse>
                {
                    Success = false,
                    Message = $"PV with ID {request.Id} already exists"
                });
            }
            
            var pv = new PV
            {
                Id = request.Id,
                Name = request.Name
            };
            
            var created = await _pvRepository.CreateAsync(pv);
            
            var response = new PVResponse
            {
                Id = created.Id,
                Name = created.Name,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };
            
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                new ApiResponse<PVResponse>
                {
                    Success = true,
                    Data = response,
                    Message = "PV created successfully"
                });
        }
        
        [HttpPut("{id}")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<PVResponse>>> Update(int id, PVRequest request)
        {
            if (id != request.Id)
            {
                return BadRequest(new ApiResponse<PVResponse>
                {
                    Success = false,
                    Message = "ID mismatch"
                });
            }
            
            var existing = await _pvRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new ApiResponse<PVResponse>
                {
                    Success = false,
                    Message = "PV not found"
                });
            }
            
            existing.Name = request.Name;
            var updated = await _pvRepository.UpdateAsync(existing);
            
            var response = new PVResponse
            {
                Id = updated.Id,
                Name = updated.Name,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            };
            
            return Ok(new ApiResponse<PVResponse>
            {
                Success = true,
                Data = response,
                Message = "PV updated successfully"
            });
        }
        
        [HttpDelete("{id}")]
        [Authorize(Roles = "superadmin")]
        public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
        {
            var deleted = await _pvRepository.DeleteAsync(id);
            
            if (!deleted)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "PV not found"
                });
            }
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "PV deleted successfully"
            });
        }
    }
}
