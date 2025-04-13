using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SistemaGestionArchivosBackend.Models;
using SistemaGestionArchivosBackend.Services;



[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocumentosController : ControllerBase
 {
    private readonly IBlobStorageService _blobService;

    public DocumentosController(IBlobStorageService blobService)
    {
        _blobService = blobService;

    }
}

