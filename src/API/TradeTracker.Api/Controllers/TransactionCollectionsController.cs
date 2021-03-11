using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
using TradeTracker.Application.Features.Transactions.Commands;
using TradeTracker.Application.Features.Transactions.Commands.CreateTransactionCollection;
using TradeTracker.Application.Features.Transactions.Queries.GetTransactionCollection;
using TradeTracker.Application.Models.Navigation;

namespace TradeTracker.Api.Controllers
{
    #pragma warning disable CS1591

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class TransactionCollectionsController : Controller
    {
        private readonly ILogger<TransactionCollectionsController> _logger;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public TransactionCollectionsController(
            IMapper mapper, 
            IMediator mediator,
            ILogger<TransactionCollectionsController> logger)
        {
            _logger = logger;
            _mapper = mapper;
            _mediator = mediator;
        }

        /// <summary>
        /// Create a collection of transactions.
        /// </summary>
        /// <param name="commandDtos">The transactions to be created</param>
        /// <remarks>
        /// Sample request: \
        /// POST /api/transactioncollections \
        /// [ \
        ///     { \
        ///         "dateTime": "2019-06-01T12:00:00", \
        ///         "symbol": "XYZ" \
        ///         "type": "SellToOpen", \
        ///         "quantity": "1", \
        ///         "notional": "50", \
        ///         "tradePrice": "50" \
        ///     }, \
        ///     { \
        ///         "dateTime": "2019-06-15T12:00:00", \
        ///         "symbol": "XYZ" \
        ///         "type": "BuyToClose", \
        ///         "quantity": "1", \
        ///         "notional": "40", \
        ///         "tradePrice": "40" \
        ///     }, \
        /// ] 
        /// </remarks>
        /// <response code="422">Validation Error</response>
        [HttpPost(Name = "CreateTransactionCollection")]
        [RequestHeaderMatchesMediaType("Content-Type", "application/json")]
        [Consumes("application/json")]
        [Produces("application/json",
            "application/vnd.trade.hateoas+json")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> CreateTransactionCollection(
            [FromBody] IEnumerable<TransactionForCreationDto> commandDtos,
            [FromHeader(Name = "Accept")] string mediaType)
        {
            _logger.LogInformation($"TransactionCollectionsController: {nameof(CreateTransactionCollection)} was called.");

            if (!MediaTypeHeaderValue.TryParse(mediaType,
                out MediaTypeHeaderValue parsedMediaType))
            {
                return BadRequest();
            }

            var command = new CreateTransactionCollectionCommand()
            {
                Transactions = _mapper.Map<IEnumerable<TransactionForCreationCommandBase>>(commandDtos)
            };
            
            var accessKey = Guid.Parse(User.FindFirstValue("AccessKey"));
            foreach (var transaction in command.Transactions)
            {
                transaction.AccessKey = accessKey;
            }

            var transactionCollection = await _mediator.Send(command);

            var idsAsString = String.Join(",", transactionCollection.Select(t => t.TransactionId));
            
            var transactionCollectionToReturn = transactionCollection.ShapeData(null);
            
            var includeLinks = parsedMediaType.SubTypeWithoutSuffix
                .EndsWith("hateoas", StringComparison.InvariantCultureIgnoreCase);
                
            if (includeLinks)
            {
                var linkedTransactionCollection = transactionCollectionToReturn
                    .Select(transaction =>
                    {
                       var transactionAsDictionary = transaction as IDictionary<string, object>;

                       var transactionLinks = CreateLinksForTransaction(
                           (Guid)transactionAsDictionary["TransactionId"], null);
                    
                       transactionAsDictionary.Add("links", transactionLinks);
                       return transactionAsDictionary;
                    });;
                
                return CreatedAtAction(
                    "GetTransactionCollection",
                    new { transactionIds = idsAsString },
                    linkedTransactionCollection);
            }
            else
            {
                return CreatedAtAction(
                    "GetTransactionCollection",
                    new { transactionIds = idsAsString },
                    transactionCollectionToReturn);
            }
        }

        /// <summary>
        /// Options for /api/transactioncollections URI.
        /// </summary>
        /// <remarks>
        /// Sample request: \
        /// OPTIONS /api/transactioncollections 
        /// </remarks>
        [HttpOptions(Name = "OptionsForTransactionCollections")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult OptionsForTransactionCollections()
        {
            _logger.LogInformation($"TransactionCollectionsController: {nameof(OptionsForTransactionCollections)} was called.");

            Response.Headers.Add("Allow", "OPTIONS,POST");
            
            return NoContent();
        }

        /// <summary>
        /// Get a collection of transactions.
        /// </summary>
        /// <param name="transactionIds">The ids for the transactions</param>
        /// <param name="fields">The fields for the transactions</param>
        /// <remarks>
        /// Sample request: \
        /// GET /api/transactions/{firstTransactionId},{secondTransactionId} 
        /// </remarks>
        /// <response code="200">Returns the requested transactions</response>
        [HttpGet("{transactionIds}", Name = "GetTransactionCollection")]
        [Consumes("application/json")]
        [Produces("application/json",
            "application/vnd.trade.hateoas+json")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> GetTransactionCollection(
            [FromRoute] [ModelBinder(BinderType = typeof(ArrayModelBinder))] IEnumerable<Guid> transactionIds,
            string fields,
            [FromHeader(Name = "Accept")] string mediaType)
        {
            _logger.LogInformation($"TransactionCollectionsController: {nameof(GetTransactionCollection)} was called.");

            if (!MediaTypeHeaderValue.TryParse(mediaType,
                out MediaTypeHeaderValue parsedMediaType))
            {
                return BadRequest();
            }

            var query = new GetTransactionCollectionQuery()
            {
                AccessKey = Guid.Parse(User.FindFirstValue("AccessKey")),
                TransactionIds = transactionIds
            };

            var returnedTransactions = await _mediator.Send(query);

            var shapedTransactions = returnedTransactions.ShapeData(fields);

            var includeLinks = parsedMediaType.SubTypeWithoutSuffix
                .EndsWith("hateoas", StringComparison.InvariantCultureIgnoreCase);

            if (includeLinks)
            {
                var shapedTransactionsWithLinks = shapedTransactions
                    .Select(transaction =>
                    {
                       var transactionAsDictionary = transaction as IDictionary<string, object>;

                       var transactionLinks = CreateLinksForTransaction(
                           (Guid)transactionAsDictionary["TransactionId"], null);
                    
                       transactionAsDictionary.Add("links", transactionLinks);
                       return transactionAsDictionary;
                    });

                var metadata = new
                {
                    resultsReturnedCount = returnedTransactions.Count()
                };

                var linkedTransactionsResource = new 
                {
                    metadata,
                    results = shapedTransactionsWithLinks
                };

                return Ok(linkedTransactionsResource);
            }
            else
            {
                return Ok(shapedTransactions);
            }
        }

        /// <summary>
        /// Options for /api/transactioncollections/{transactionIds} URI.
        /// </summary>
        /// <remarks>
        /// Sample request: \
        /// OPTIONS /api/transactioncollections/{firstTransactionId},{secondTransactionId} 
        /// </remarks>
        [HttpOptions("{transactionIds}", Name = "OptionsForTransactionCollectionByTransactionIds")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult OptionsForTransactionCollectionByTransactionIds()
        {
            _logger.LogInformation($"TransactionCollectionsController: {nameof(OptionsForTransactionCollectionByTransactionIds)} was called.");

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
    }

    #pragma warning restore CS1591
}