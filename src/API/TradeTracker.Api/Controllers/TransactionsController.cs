using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TradeTracker.Api.ActionConstraints;
using TradeTracker.Api.Helpers;
using TradeTracker.Api.Models.Pagination;
using TradeTracker.Api.Utilities;
using TradeTracker.Application.Features.Transactions;
using TradeTracker.Application.Features.Transactions.Commands.CreateTransaction;
using TradeTracker.Application.Features.Transactions.Commands.DeleteTransaction;
using TradeTracker.Application.Features.Transactions.Commands.PatchTransaction;
using TradeTracker.Application.Features.Transactions.Commands.UpdateTransaction;
using TradeTracker.Application.Features.Transactions.Queries.ExportTransactions;
using TradeTracker.Application.Features.Transactions.Queries.GetTransaction;
using TradeTracker.Application.Features.Transactions.Queries.GetTransactions;
using TradeTracker.Application.Models.Navigation;

namespace TradeTracker.Api.Controllers
{
    #pragma warning disable CS1591

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class TransactionsController : Controller
    {
        private readonly ILogger<TransactionsController> _logger;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public TransactionsController(
            IMapper mapper, 
            IMediator mediator,
            ILogger<TransactionsController> logger)
        {
            _logger = logger;
            _mapper = mapper;
            _mediator = mediator;
        }

        /// <summary>
        /// Get a paged list of transactions.
        /// </summary>
        /// <param name="parameters">The resource parameters for specifying the returned transactions</param>
        /// <remarks>
        /// Sample request: \
        /// GET /api/transactions \
        /// { \
        ///     "orderBy": "DateTime", \
        ///     "pageNumber": "2" \
        ///     "pageSize": "50", \
        ///     "including": [ \ 
        ///         "ABC", \
        ///         "XYZ", \
        ///         "MNO" \
        ///     ], \
        ///     "rangeStart": "2020-06-01T00:00:00", \
        ///     "rangeEnd": "2021-01-01T00:00:00" \
        /// } \
        /// </remarks>
        /// <response code="200">Returns any paged transactions matching parameters</response>
        /// <response code="422">Validation Error</response>
        [HttpGet(Name = "GetPagedTransactions")]
        [Consumes("application/json")]
        [Produces("application/json",
            "application/vnd.trade.hateoas+json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [RequestHeaderMatchesMediaType("Content-Type", "application/json")]
        [RequestHeaderMatchesMediaType("Accept", "application/json")]
        public async Task<ActionResult<IEnumerable<TransactionForReturnDto>>> GetPagedTransactions(
            [FromQuery] GetPagedTransactionsResourceParameters parameters)
        {
            _logger.LogInformation($"TransactionsController: {nameof(GetPagedTransactions)} was called.");

            var query = _mapper.Map<GetTransactionsQuery>(parameters);
            query.AccessKey = Guid.Parse(User.FindFirstValue("AccessKey"));

            var pagedTransactionsBase = await _mediator.Send(query);

            Response.Headers.Add("X-Paging-PageNumber", pagedTransactionsBase.CurrentPage.ToString());
            Response.Headers.Add("X-Paging-PageSize", pagedTransactionsBase.PageSize.ToString());
            Response.Headers.Add("X-Paging-PageCount", pagedTransactionsBase.TotalPages.ToString());
            Response.Headers.Add("X-Paging-TotalRecordCount", pagedTransactionsBase.TotalCount.ToString());

            return Ok(pagedTransactionsBase.Items);
        }

        /// <summary>
        /// Get a paged list of transactions.
        /// </summary>
        /// <param name="parameters">The resource parameters for specifying the returned transactions</param>
        /// <remarks>
        /// Sample request: \
        /// GET /api/transactions \
        /// { \
        ///     "orderBy": "DateTime", \
        ///     "pageNumber": "2" \
        ///     "pageSize": "50", \
        ///     "including": [ \ 
        ///         "ABC", \
        ///         "XYZ", \
        ///         "MNO" \
        ///     ], \
        ///     "rangeStart": "2020-06-01T00:00:00", \
        ///     "rangeEnd": "2021-01-01T00:00:00" \
        /// } \
        /// </remarks>
        /// <response code="200">Returns any paged transactions matching parameters</response>
        /// <response code="422">Validation Error</response>
        [HttpGet(Name = "GetPagedTransactionsWithLinks")]
        [Consumes("application/json")]
        [Produces("application/vnd.trade.hateoas+json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity,
            Type = typeof(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary))]
        [RequestHeaderMatchesMediaType("Content-Type", "application/json")]
        [RequestHeaderMatchesMediaType("Accept", "application/vnd.trade.hateoas+json")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult<PagedTransactionsWithLinksDto>> GetPagedTransactionsWithLinks(
            [FromQuery] GetPagedTransactionsResourceParameters parameters)
        {
            _logger.LogInformation($"TransactionsController: {nameof(GetPagedTransactionsWithLinks)} was called.");

            var query = _mapper.Map<GetTransactionsQuery>(parameters);
            query.AccessKey = Guid.Parse(User.FindFirstValue("AccessKey"));

            var pagedTransactionsBase = await _mediator.Send(query);

            var pagedTransactionsWithLinks = new PagedTransactionsWithLinksDto();

            pagedTransactionsWithLinks.Items = _mapper
            .Map<IEnumerable<TransactionWithLinksForReturnDto>>(pagedTransactionsBase.Items);

            pagedTransactionsWithLinks.Items = pagedTransactionsWithLinks.Items
                .Select(transaction =>
                {
                    transaction.Links = CreateLinksForTransaction(
                        transaction.TransactionId, null);

                    return transaction;
                });
            
            pagedTransactionsWithLinks.Links = 
                CreateLinksForTransactions(
                    parameters,
                    pagedTransactionsBase.HasNext,
                    pagedTransactionsBase.HasPrevious);
            
            pagedTransactionsWithLinks.Metadata =
                new PaginationMetadata()
                {
                    PageNumber = pagedTransactionsBase.CurrentPage,
                    PageSize = pagedTransactionsBase.PageSize,
                    PageCount = pagedTransactionsBase.TotalPages,
                    TotalRecordCount = pagedTransactionsBase.TotalCount,
                };

            return Ok(pagedTransactionsWithLinks);
        }

        /// <summary>
        /// Create a transaction.
        /// </summary>
        /// <param name="commandDto">The transaction to be created</param>
        /// <remarks>
        /// Sample request: \
        /// POST /api/transactions \
        /// { \
        ///     "dateTime": "2020-01-01T12:30:15", \
        ///     "symbol": "ABC" \
        ///     "type": "BuyToOpen", \
        ///     "quantity": "2.5", \
        ///     "notional": "13.75", \
        ///     "tradePrice": "5.50" \
        /// } \
        /// </remarks>
        /// <response code="422">Validation Error</response>
        [HttpPost(Name = "CreateTransaction")]
        [Consumes("application/json")]
        [Produces("application/vnd.trade.hateoas+json")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [RequestHeaderMatchesMediaType("Content-Type", "application/json")]
        [RequestHeaderMatchesMediaType("Accept", "application/vnd.trade.hateoas+json")]
        public async Task<IActionResult> CreateTransaction(
            [FromBody] CreateTransactionCommandDto commandDto)
        {
            _logger.LogInformation($"TransactionsController: {nameof(CreateTransaction)} was called.");

            var command = _mapper.Map<CreateTransactionCommand>(commandDto);
            command.AccessKey = Guid.Parse(User.FindFirstValue("AccessKey"));

            var createdTransaction = await _mediator.Send(command);

            var linkedTransaction = createdTransaction
                .ShapeData(null) as IDictionary<string, object>;
                
            var links = CreateLinksForTransaction(
                (Guid)linkedTransaction["TransactionId"], null);
            
            linkedTransaction.Add("links", links);

            return CreatedAtAction(
                "GetTransaction",
                new { transactionId = linkedTransaction["TransactionId"] },
                linkedTransaction);
        }

        /// <summary>
        /// Options for /api/transactions URI.
        /// </summary>
        /// <remarks>
        /// Sample request: \
        /// OPTIONS /api/transactions \
        /// </remarks>
        [HttpOptions(Name = "OptionsForTransactions")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult OptionsForTransactions()
        {
            _logger.LogInformation($"TransactionsController: {nameof(OptionsForTransactions)} was called.");
            
            Response.Headers.Add("Allow", "GET,OPTIONS,POST");
            
            return NoContent();
        }

        /// <summary>
        /// Get a transaction by its id.
        /// </summary>
        /// <param name="transactionId">The id of the transaction</param>
        /// <remarks>
        /// Sample request: \
        /// GET /api/transactions/{transactionId} 
        /// </remarks>
        /// <response code="200">Returns the requested transaction</response>
        [HttpGet("{transactionId}", Name = "GetTransaction")]
        [Consumes("application/json")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [RequestHeaderMatchesMediaType("Content-Type", "application/json")]
        [RequestHeaderMatchesMediaType("Accept", "application/json")]
        public async Task<ActionResult<TransactionForReturnDto>> GetTransaction(
            [FromRoute] Guid transactionId)
        {
            _logger.LogInformation($"TransactionsController: {nameof(GetTransaction)} was called.");

            var query = new GetTransactionQuery()
            {
                AccessKey = Guid.Parse(User.FindFirstValue("AccessKey")),
                TransactionId = transactionId
            };
            
            var transaction = await _mediator.Send(query);

            return Ok(transaction);
        }

        /// <summary>
        /// Get a transaction by its id.
        /// </summary>
        /// <param name="transactionId">The id of the transaction</param>
        /// <remarks>
        /// Sample request: \
        /// GET /api/transactions/{transactionId} 
        /// </remarks>
        /// <response code="200">Returns the requested transaction</response>
        [HttpGet("{transactionId}", Name = "GetTransactionWithLinks")]
        [Consumes("application/json")]
        [Produces("application/vnd.trade.hateoas+json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [RequestHeaderMatchesMediaType("Content-Type", "application/json")]
        [RequestHeaderMatchesMediaType("Accept", "application/vnd.trade.hateoas+json")]
        public async Task<ActionResult<TransactionWithLinksForReturnDto>> GetTransactionWithLinks(
            [FromRoute] Guid transactionId)
        {
            _logger.LogInformation($"TransactionsController: {nameof(GetTransactionWithLinks)} was called.");

            var query = new GetTransactionQuery()
            {
                AccessKey = Guid.Parse(User.FindFirstValue("AccessKey")),
                TransactionId = transactionId
            };
            
            var transaction = await _mediator.Send(query);

            var transactionWithLinks = _mapper.Map<TransactionWithLinksForReturnDto>(transaction);

            transactionWithLinks.Links = CreateLinksForTransaction(transactionId, null);

            return Ok(transactionWithLinks);
        }

        /// <summary>
        /// Update a transaction.
        /// </summary>
        /// <param name="transactionId">The id for the transaction to be updated</param>
        /// <param name="commandDto">The transaction with updated values</param>
        /// <response code="422">Validation Error</response>
        /// <remarks>
        /// Sample request: \
        /// PUT /api/transactions/{transactionId} \
        /// { \
        ///     "dateTime": "2020-01-15T15:00:00", \
        ///     "symbol": "CBA" \
        ///     "type": "SellToClose", \
        ///     "quantity": "1.0", \
        ///     "notional": "12.50", \
        ///     "tradePrice": "12.50" \
        /// } 
        /// </remarks>
        [HttpPut("{transactionId}", Name = "UpdateTransaction")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult> UpdateTransaction(
            [FromRoute] Guid transactionId, 
            [FromBody] UpdateTransactionCommandDto commandDto)
        {
            _logger.LogInformation($"TransactionsController: {nameof(UpdateTransaction)} was called.");

            var command = _mapper.Map<UpdateTransactionCommand>(commandDto);
            command.AccessKey = Guid.Parse(User.FindFirstValue("AccessKey"));
            command.TransactionId = transactionId;

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>
        /// Partially update a transaction.
        /// </summary>
        /// <param name="transactionId">The id for the transaction to be updated</param>
        /// <param name="patchDocument">The set of operations to be applied to the transaction</param>
        /// Sample request: \
        /// PATCH /api/transactions/{transactionId} \
        /// [ \
        ///     { \
        ///         "op": "replace", \
        ///         "path": "/quantity", \
        ///         "value": "25" \
        ///     }, \
        ///     { \
        ///         "op": "replace", \
        ///         "path": "/notional",
        ///         "value": "75" \
        ///     } \
        /// ]
        /// <response code="422">Validation Error</response>
        [HttpPatch("{transactionId}", Name = "PatchTransaction")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult> PatchTransaction(
            [FromRoute] Guid transactionId, 
            JsonPatchDocument<UpdateTransactionCommandDto> patchDocument)
        {
            _logger.LogInformation($"TransactionsController: {nameof(PatchTransaction)} was called.");

            var command = new PatchTransactionCommand()
            {
                AccessKey = Guid.Parse(User.FindFirstValue("AccessKey")),
                TransactionId = transactionId,
                PatchDocument = patchDocument
            };

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>
        /// Delete a transaction.
        /// </summary>
        /// <param name="transactionId">The id for the transaction to be deleted</param>
        /// <remarks>
        /// Sample request: \
        /// DELETE /api/transactions/{transactionId} 
        /// </remarks>
        [HttpDelete("{transactionId}", Name = "DeleteTransaction")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteTransaction(
            [FromRoute] Guid transactionId)
        {
            _logger.LogInformation($"TransactionsController: {nameof(DeleteTransaction)} was called.");

            var command = new DeleteTransactionCommand() 
            {
                AccessKey = Guid.Parse(User.FindFirstValue("AccessKey")),
                TransactionId = transactionId
            };

            await _mediator.Send(command);
            return NoContent();
        }

        /// <summary>
        /// Options for /api/transactions/{transactionId} URI.
        /// </summary>
        /// <remarks>
        /// Sample request: \
        /// OPTIONS /api/transactions/{transactionId} 
        /// </remarks>
        [HttpOptions("{transactionId}", Name = "OptionsForTransactionById")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult OptionsForTransactionById()
        {
            _logger.LogInformation($"TransactionsController: {nameof(OptionsForTransactionById)} was called.");

            Response.Headers.Add("Allow", "DELETE,GET,OPTIONS,PATCH,PUT");
            
            return NoContent();
        }

        /// <summary>
        /// Get all transactions as a CSV file.
        /// </summary>
        /// <remarks>
        /// Sample request: \
        /// GET /api/transactions/export 
        /// </remarks>
        /// <response code="200"></response>
        [HttpGet("export", Name = "ExportTransactions")]
        [FileResultContentType("text/csv")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportTransactions()
        {
            _logger.LogInformation($"TransactionsController: {nameof(ExportTransactions)} was called.");

            var query = new ExportTransactionsQuery()
            {
                AccessKey = Guid.Parse(User.FindFirstValue("AccessKey"))
            };

            var fileExportDto = await _mediator.Send(query);
            return File(
                fileExportDto.Data, 
                fileExportDto.ContentType, 
                fileExportDto.TransactionsExportFileName);
        }

        /// <summary>
        /// Options for /api/transactions/export URI.
        /// </summary>
        /// <remarks>
        /// Sample request: \
        /// OPTIONS /api/transactions/export 
        /// </remarks>
        [HttpOptions("export", Name = "OptionsForExportTransactions")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult OptionsForExportTransactions()
        {
            _logger.LogInformation($"TransactionsController: {nameof(OptionsForExportTransactions)} was called.");

            Response.Headers.Add("Allow", "GET,OPTIONS");
            
            return NoContent();
        }

        private IEnumerable<LinkDto> CreateLinksForTransaction(
            Guid transactionId, 
            string fields)
        {
            var links = new List<LinkDto>();

            if (String.IsNullOrWhiteSpace(fields))
            {
                links.Add(
                    new LinkDto(
                        Url.Link(
                            "GetTransaction", 
                            new { transactionId }),
                    "self",
                    "GET"));
            }
            else
            {
                links.Add(
                    new LinkDto(
                        Url.Link(
                            "GetTransaction", 
                            new { transactionId, fields }),
                    "self",
                    "GET"));
            }

            links.Add(
                new LinkDto(
                    Url.Link(
                        "UpdateTransaction",
                        new { transactionId }),
                    "update transaction",
                    "PUT"));
                
            links.Add(
                new LinkDto(
                    Url.Link(
                        "PatchTransaction",
                        new { transactionId }),
                    "patch transaction",
                    "PATCH"));

            links.Add(
                new LinkDto(
                    Url.Link(
                        "DeleteTransaction",
                        new { transactionId }),
                    "delete transaction",
                    "DELETE"));

            return links;
        }

        private IEnumerable<LinkDto> CreateLinksForTransactions(
            GetPagedTransactionsResourceParameters parameters,
            bool hasNext,
            bool hasPrevious)
        {
            var links = new List<LinkDto>();

            links.Add(
                new LinkDto(
                    CreateTransactionsResourceUrl(
                        parameters, 
                        ResourceUriType.CurrentPage),
                    "self",
                    "GET"));

            if (hasNext)
            {
                links.Add(
                    new LinkDto(
                        CreateTransactionsResourceUrl(
                            parameters, 
                            ResourceUriType.NextPage),
                        "nextPage",
                        "GET"));
            }

            if (hasPrevious)
            {
                links.Add(
                    new LinkDto(
                        CreateTransactionsResourceUrl(
                            parameters, 
                            ResourceUriType.PreviousPage),
                        "previousPage",
                        "GET"));
            }

            return links;
        }

        private string CreateTransactionsResourceUrl(
            GetPagedTransactionsResourceParameters parameters,
            ResourceUriType type)
        {
            switch (type)
            {
                case ResourceUriType.PreviousPage:
                    return Url.Link(
                        "GetTransactions",
                        new
                        {
                            orderBy = parameters.OrderBy,
                            pageNumber = parameters.PageNumber - 1,
                            pageSize = parameters.PageSize,
                            including = parameters.Including,
                            excluding = parameters.Excluding,
                            rangeStart = parameters.RangeStart,
                            rangeEnd = parameters.RangeEnd
                        });

                case ResourceUriType.NextPage:
                    return Url.Link(
                        "GetTransactions",
                        new
                        {
                            orderBy = parameters.OrderBy,
                            pageNumber = parameters.PageNumber + 1,
                            pageSize = parameters.PageSize,
                            including = parameters.Including,
                            excluding = parameters.Excluding,
                            rangeStart = parameters.RangeStart,
                            rangeEnd = parameters.RangeEnd
                        });

                case ResourceUriType.CurrentPage:
                default:
                    return Url.Link(
                        "GetTransactions",
                        new
                        {
                            orderBy = parameters.OrderBy,
                            pageNumber = parameters.PageNumber,
                            pageSize = parameters.PageSize,
                            including = parameters.Including,
                            excluding = parameters.Excluding,
                            rangeStart = parameters.RangeStart,
                            rangeEnd = parameters.RangeEnd
                        });
            }
        }        
    }

    #pragma warning restore CS1591
}