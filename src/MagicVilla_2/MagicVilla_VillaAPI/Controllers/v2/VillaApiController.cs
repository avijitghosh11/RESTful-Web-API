﻿using AutoMapper;
using MagicVilla_VillaAPI.Models;
using MagicVilla_VillaAPI.Models.Dtos;
using MagicVilla_VillaAPI.Repository.IRepostiory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace MagicVilla_VillaAPI.Controllers.v2
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class VillaApiController : ControllerBase
    {
        protected APIResponse _response;
        private readonly ILogger<VillaApiController> _logger;
        private readonly IMapper _mapper;
        private readonly IVillaRepository _villaRepository;

        public VillaApiController(ILogger<VillaApiController> logger, IMapper mapper, IVillaRepository villaRepository)
        {
            _logger = logger;
            _mapper = mapper;
            _villaRepository = villaRepository;
            _response = new();
        }
        //private readonly ILogging _logger;
        //public VillaApiController(ILogging logger)
        //{
        //    _logger = logger;
        //}

        [HttpGet]
        //[ResponseCache(CacheProfileName = "Default30")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<APIResponse>> GetVillas([FromQuery(Name = "filterOccupancy")] int? occupancy,
            [FromQuery]string? search, [FromQuery] int pageNumber=1, [FromQuery] int pageSize=0)
        {
            try
            {
                _logger.LogInformation("Get all villas", "info");
                IEnumerable<Villa> villas;
                if (occupancy > 0)
                {
                    villas = await _villaRepository.GetAllAsync(x => x.Occupancy == occupancy, pageNumber: pageNumber, pageSize: pageSize);
                }
                else
                {
                    villas = await _villaRepository.GetAllAsync(pageNumber: pageNumber,pageSize: pageSize);
                }

                if (!string.IsNullOrEmpty(search))
                {
                    villas = villas.Where(x=>x.Name.ToLower().Contains(search.ToLower()));
                }

                Pagination pagination = new() { PageNumber = pageNumber,PageSize=pageSize};
                Response.Headers.Add("X-Pagination", JsonSerializer.Serialize(pagination));

                _response.Result = _mapper.Map<List<VillaDto>>(villas);
                _response.StatusCode = HttpStatusCode.OK;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages
                     = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        [HttpGet("{id:int}", Name = "GetVilla")]
        //[ResponseCache(CacheProfileName = "Default30")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<APIResponse>> GetVilla(int id)
        {
            try
            {
                if (id == 0)
                {
                    _logger.LogError("No villa found with id {0}", id);
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.ErrorMessages = new List<string>() { "No villa found with id " + id };
                    _response.IsSuccess = false;
                    return BadRequest(_response);
                }

                Villa villa = await _villaRepository.GetAsync(x => x.Id == id);
                if (villa == null)
                {
                    _logger.LogError("No villa found with id {0}", id);
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.ErrorMessages = new List<string>() { "No villa found with id " + id };
                    _response.IsSuccess = false;
                    return NotFound(_response);
                }
                _logger.LogInformation("Villa found with id {0}", id);
                _response.StatusCode = HttpStatusCode.OK;
                _response.Result = _mapper.Map<VillaDto>(villa);
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages
                     = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<ActionResult<APIResponse>> CreateVilla([FromForm] VillaCreateDTO createDTO)
        {
            try
            {
                if (createDTO == null)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.ErrorMessages = new List<string>() { "Data is required for creating a villa" };
                    _response.IsSuccess = false;
                    _response.Result = createDTO;
                    return BadRequest(_response);
                }
                else if (await _villaRepository.GetAsync(x => x.Name.ToLower() == createDTO.Name.ToLower()) != null)
                {
                    ModelState.AddModelError("ErrorMessages", "Villa name must be unique");
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.ErrorMessages = new List<string>() { "Villa name must be unique" };
                    _response.IsSuccess = false;
                    _response.Result = ModelState;
                    return BadRequest(_response);
                }
                Villa villa = _mapper.Map<Villa>(createDTO);
                villa.CreatedDate = DateTime.Now;
                villa.UpdatedDate = DateTime.Now;
                await _villaRepository.CreateAsync(villa);

                if (createDTO.Image != null)
                {
                    string fileName = villa.Id + Path.GetExtension(createDTO.Image.FileName);
                    string filePath = @"wwwroot\ProductImage\" + fileName;
                    var directoryLocation = Path.Combine(Directory.GetCurrentDirectory(), filePath);
                    FileInfo file = new FileInfo(directoryLocation);

                    if (file.Exists)
                    {
                        file.Delete();
                    }

                    using (var fileStream = new FileStream(directoryLocation, FileMode.Create))
                    {
                        createDTO.Image.CopyTo(fileStream);
                    }

                    var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.PathBase.Value}";
                    villa.ImageUrl = baseUrl + "/ProductImage/" + fileName;
                    villa.ImageLocalPath = filePath;

                }
                else
                {
                    villa.ImageUrl = "https://placehold.co/600x400";
                }

                await _villaRepository.UpdateAsync(villa);

                _response.StatusCode = HttpStatusCode.Created;
                _response.Result = _mapper.Map<VillaDto>(villa);
                return CreatedAtRoute("GetVilla", new { id = villa.Id }, _response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages
                     = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        [HttpDelete("{id:int}", Name = "DeleteVilla")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<APIResponse>> DeleteVilla(int id)
        {
            try
            {
                if (id == 0)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.ErrorMessages = new List<string>() { "No villa found with id " + id };
                    _response.IsSuccess = false;
                    return BadRequest(_response);
                }

                Villa villa = await _villaRepository.GetAsync(x => x.Id == id);
                if (villa == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.ErrorMessages = new List<string>() { "No villa found with id " + id };
                    _response.IsSuccess = false;
                    _response.Result = villa;
                    return NotFound(_response);
                }

                if (!string.IsNullOrEmpty(villa.ImageLocalPath))
                {
                    var oldFilePathDirectory = Path.Combine(Directory.GetCurrentDirectory(), villa.ImageLocalPath);
                    FileInfo file = new FileInfo(oldFilePathDirectory);

                    if (file.Exists)
                    {
                        file.Delete();
                    }
                }

                await _villaRepository.RemoveAsync(villa);
                _response.StatusCode = HttpStatusCode.NoContent;
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages
                     = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        [HttpPut("{id:int}", Name = "UpdateVilla")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<APIResponse>> UpdateVilla(int id, [FromForm] VillaUpdateDTO updateDTO)
        {
            try
            {
                if (updateDTO == null || id != updateDTO.Id)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.ErrorMessages = new List<string>() { "This is a bad request" };
                    _response.IsSuccess = false;
                    _response.Result = updateDTO;
                    return BadRequest(_response);
                }

                Villa villaObj = await _villaRepository.GetAsync(x => x.Id == id, tracked: false);

                if (villaObj == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.ErrorMessages = new List<string>() { "No villa found with id " + id };
                    _response.IsSuccess = false;
                    _response.Result = villaObj;
                    return NotFound(_response);
                }
                Villa data = _mapper.Map<Villa>(updateDTO);
                data.CreatedDate = villaObj.CreatedDate;
                data.UpdatedDate = DateTime.Now;

                if (updateDTO.Image != null)
                {
                    if (!string.IsNullOrEmpty(data.ImageLocalPath))
                    {
                        var oldFilePathDirectory = Path.Combine(Directory.GetCurrentDirectory(), data.ImageLocalPath);
                        FileInfo file = new FileInfo(oldFilePathDirectory);

                        if (file.Exists)
                        {
                            file.Delete();
                        }
                    }

                    string fileName = updateDTO.Id + Path.GetExtension(updateDTO.Image.FileName);
                    string filePath = @"wwwroot\ProductImage\" + fileName;

                    var directoryLocation = Path.Combine(Directory.GetCurrentDirectory(), filePath);

                    using (var fileStream = new FileStream(directoryLocation, FileMode.Create))
                    {
                        updateDTO.Image.CopyTo(fileStream);
                    }

                    var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.PathBase.Value}";
                    data.ImageUrl = baseUrl + "/ProductImage/" + fileName;
                    data.ImageLocalPath = filePath;

                }
                else
                {
                    if (villaObj.ImageUrl != null)
                    {
                        data.ImageUrl = villaObj.ImageUrl;
                        data.ImageLocalPath= villaObj.ImageLocalPath;
                    }
                    else
                    {
                        data.ImageUrl = "https://placehold.co/600x400";
                    }
                    
                }

                Villa result = await _villaRepository.UpdateAsync(data);
                _response.StatusCode = HttpStatusCode.NoContent;
                _response.Result = _mapper.Map<VillaUpdateDTO>(result);
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages
                     = new List<string>() { ex.ToString() };
            }
            return _response;
        }

        [HttpPatch("{id:int}", Name = "UpdatePartialVilla")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<APIResponse>> UpdatePartialVilla(int id, JsonPatchDocument<VillaUpdateDTO> patchDTO)
        {
            try
            {
                if (patchDTO == null || id == 0)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.ErrorMessages = new List<string>() { "This is a bad request" };
                    _response.IsSuccess = false;
                    _response.Result = patchDTO;
                    return BadRequest(_response);
                }
                Villa villaObj = await _villaRepository.GetAsync(x => x.Id == id, tracked: false);

                if (villaObj == null)
                {
                    _response.StatusCode = HttpStatusCode.NotFound;
                    _response.ErrorMessages = new List<string>() { "No villa found with id " + id };
                    _response.IsSuccess = false;
                    _response.Result = villaObj;
                    return NotFound(_response);
                }

                VillaUpdateDTO villaUpdateDTO = _mapper.Map<VillaUpdateDTO>(villaObj);

                patchDTO.ApplyTo(villaUpdateDTO, ModelState);

                if (!ModelState.IsValid)
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    _response.Result = ModelState;
                    return BadRequest(_response);
                }

                Villa data = _mapper.Map<Villa>(villaUpdateDTO);
                data.UpdatedDate = DateTime.Now;
                data.CreatedDate = villaObj.CreatedDate;

                Villa result = await _villaRepository.UpdateAsync(data);
                _response.StatusCode = HttpStatusCode.NoContent;
                _response.Result = _mapper.Map<VillaUpdateDTO>(result);
                return Ok(_response);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.ErrorMessages
                     = new List<string>() { ex.ToString() };
            }
            return _response;
        }
    }
}
