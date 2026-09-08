using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesApp.Attributes;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Services;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SurveysController : ControllerBase
    {
        private readonly ISurveyService _surveyService;
        private readonly IMessageService _messageService;

        public SurveysController(ISurveyService surveyService, IMessageService messageService)
        {
            _surveyService = surveyService;
            _messageService = messageService;
        }

        // POST: api/surveys (Superadmin only)
        [HttpPost]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<SurveySummaryDto>>> Create([FromBody] CreateSurveyDto dto)
        {
            if (!TryGetCurrentUserId(out var creatorId))
            {
                return Unauthorized(new ApiResponse<SurveySummaryDto>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.Unauthorized)
                });
            }

            try
            {
                var summary = await _surveyService.CreateAndDispatchAsync(dto, creatorId);
                return StatusCode(201, new ApiResponse<SurveySummaryDto>
                {
                    Success = true,
                    Data = summary,
                    Message = _messageService.Get(AppMessage.SurveyCreatedSuccessfully)
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<SurveySummaryDto>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: api/surveys (Superadmin only)
        [HttpGet]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<List<SurveySummaryDto>>>> GetAll()
        {
            var surveys = await _surveyService.GetAllSurveysAsync();
            return Ok(new ApiResponse<List<SurveySummaryDto>>
            {
                Success = true,
                Data = surveys,
                Message = _messageService.Get(AppMessage.SurveysRetrievedSuccessfully)
            });
        }

        // GET: api/surveys/{id}/results (Superadmin only)
        [HttpGet("{id}/results")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<SurveyResultDto>>> GetResults(Guid id)
        {
            try
            {
                var results = await _surveyService.GetSurveyResultsAsync(id);
                return Ok(new ApiResponse<SurveyResultDto>
                {
                    Success = true,
                    Data = results,
                    Message = _messageService.Get(AppMessage.SurveyResultsRetrievedSuccessfully)
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<SurveyResultDto>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // POST: api/surveys/{id}/resend (Superadmin only)
        [HttpPost("{id}/resend")]
        [HasPermission("system:superadmin")]
        public async Task<ActionResult<ApiResponse<string>>> Resend(Guid id, [FromBody] ResendSurveyDto dto)
        {
            try
            {
                await _surveyService.ResendAsync(id, dto);
                return Ok(new ApiResponse<string>
                {
                    Success = true,
                    Message = _messageService.Get(AppMessage.SurveyResentSuccessfully)
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: api/surveys/pending (Any authenticated user)
        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<List<SurveyAssignmentDto>>>> GetPending()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<List<SurveyAssignmentDto>>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.Unauthorized)
                });
            }

            var pending = await _surveyService.GetPendingForUserAsync(userId);
            return Ok(new ApiResponse<List<SurveyAssignmentDto>>
            {
                Success = true,
                Data = pending,
                Message = "Perguntas pendentes recuperadas com sucesso"
            });
        }

        // POST: api/surveys/answer (Any authenticated user)
        [HttpPost("answer")]
        public async Task<ActionResult<ApiResponse<string>>> Answer([FromBody] AnswerSurveyDto dto)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.Unauthorized)
                });
            }

            try
            {
                await _surveyService.AnswerAsync(userId, dto);
                return Ok(new ApiResponse<string>
                {
                    Success = true,
                    Message = _messageService.Get(AppMessage.SurveyAnsweredSuccessfully)
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<string>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        // GET: api/surveys/my-history (Any authenticated user)
        [HttpGet("my-history")]
        public async Task<ActionResult<ApiResponse<List<UserSurveyHistoryDto>>>> GetMyHistory()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<List<UserSurveyHistoryDto>>
                {
                    Success = false,
                    Message = _messageService.Get(AppMessage.Unauthorized)
                });
            }

            var history = await _surveyService.GetUserHistoryAsync(userId);
            return Ok(new ApiResponse<List<UserSurveyHistoryDto>>
            {
                Success = true,
                Data = history,
                Message = "Histórico de perguntas recuperado com sucesso"
            });
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdString, out userId);
        }
    }
}
