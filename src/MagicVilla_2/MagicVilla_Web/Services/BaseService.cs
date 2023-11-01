﻿using MagicVilla_Utility;
using MagicVilla_Web.Models;
using MagicVilla_Web.Services.IServices;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace MagicVilla_Web.Services
{
    public class BaseService : IBaseService
    {
        public IHttpClientFactory _httpClient { get; set; }
        public APIResponse responseModel { get; set; }
        //private readonly ITokenProvider _tokenProvider;
        //private readonly IApiMessageRequestBuilder _apiMessageRequestBuilder;
        protected readonly string VillaApiUrl;
        private IHttpContextAccessor _httpContextAccessor;
        public BaseService(IHttpClientFactory httpClient)
        {
            this.responseModel = new();
            this._httpClient = httpClient;
        }
        public async Task<T> SendAsync<T>(APIRequest request)
        {
            try
            {
                var client = _httpClient.CreateClient("MagicVillaApi");
                HttpRequestMessage message = new HttpRequestMessage();
                message.RequestUri = new Uri(request.Url);

                if (request.ContentType == SD.ContentType.MultipartFormData)
                {
                    message.Headers.Add("Accept", "*/*");
                    var content = new MultipartFormDataContent();
                    foreach(var prop in request.Data.GetType().GetProperties())
                    {
                        var value  = prop.GetValue(request.Data);
                        if(value is FormFile)
                        {
                            var file = (FormFile)value;
                            if (file != null)
                            {
                                content.Add(new StreamContent(file.OpenReadStream()),prop.Name,file.FileName);
                            }
                        }
                        else
                        {
                            content.Add(new StringContent(value==null?"":value.ToString()), prop.Name);
                        }
                    }
                    message.Content = content;
                }
                else
                {
                    message.Headers.Add("Accept", "application/json");
                    if (request.Data != null)
                    {
                        message.Content = new StringContent(JsonConvert.SerializeObject(request.Data),
                            Encoding.UTF8, "application/json");
                    }
                }

                
                switch (request.ApiType)
                {
                    case SD.ApiType.POST:
                        message.Method = HttpMethod.Post;
                        break;
                    case SD.ApiType.PUT:
                        message.Method = HttpMethod.Put;
                        break;
                    case SD.ApiType.DELETE:
                        message.Method = HttpMethod.Delete;
                        break;
                    default:
                        message.Method = HttpMethod.Get;
                        break;
                }

                HttpResponseMessage response = null;
                if (!string.IsNullOrEmpty(request.Token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.Token);
                }
                
				response = await client.SendAsync(message);
                var apiContent = await response.Content.ReadAsStringAsync();
                try
                {
                    APIResponse ApiResponse = JsonConvert.DeserializeObject<APIResponse>(apiContent);
                    if (ApiResponse != null && (response.StatusCode == System.Net.HttpStatusCode.BadRequest
                        || response.StatusCode == System.Net.HttpStatusCode.NotFound))
                    {
                        ApiResponse.StatusCode = System.Net.HttpStatusCode.BadRequest;
                        ApiResponse.IsSuccess = false;
                        var res = JsonConvert.SerializeObject(ApiResponse);
                        var returnObj = JsonConvert.DeserializeObject<T>(res);
                        return returnObj;
                    }
                }
                catch (Exception e)
                {
                    var exceptionResponse = JsonConvert.DeserializeObject<T>(apiContent);
                    return exceptionResponse;
                }
                var apiResponse = JsonConvert.DeserializeObject<T>(apiContent);
                return apiResponse;
            }
            catch (Exception ex)
            {
                var dto = new APIResponse()
                {
                    ErrorMessages = new List<string>(){ Convert.ToString(ex.Message) } ,
                    IsSuccess = false
                };
                var res = JsonConvert.SerializeObject(dto);
                var response = JsonConvert.DeserializeObject<T>(res);
                return response;
            }
        }
    }
}
