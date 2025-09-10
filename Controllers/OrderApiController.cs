using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.BillingPortal;
using Stripe.Checkout;
using VDVT.Order.Services.Api.Contract;
using VDVT.Order.Services.Api.Data;
using VDVT.Order.Services.Api.Models;
using VDVT.Order.Services.Api.Models.Dto;
using VDVT.Order.Services.Api.Utility;

namespace VDVT.Order.Services.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderApiController : ControllerBase
    {
        protected ResponseDto _response;
        private IMapper _mapper;
        private readonly AppDbContext _appDbContext;
        IProductoService _productoService;
        //private readonly IMessageBus _messageBus;
        //private readonly IRabbmitMQOrderMessageSender _messageBus;
        private readonly IConfiguration _configuration;

        public OrderApiController(IMapper mapper, AppDbContext appDbContext, IProductoService productoService, IConfiguration configuration)
        {
            _appDbContext = appDbContext;
            _productoService = productoService;
            _response = new ResponseDto();
            _mapper = mapper;
            _configuration = configuration;
        }

        [Authorize]
        [HttpGet("GetOrders")]
        public ResponseDto Get(string userId = "")
        {
            try
            {
                IEnumerable<OrderHeader> objectList;
                if (User.IsInRole(Utility.Utilities.RoleAdmin))
                {
                    objectList = _appDbContext.OrderHeaders.Include(u => u.OrderDetailsDto)
                        .OrderByDescending(u => u.OrderHeaderId)
                        .ToList();
                }
                else
                {
                    objectList = _appDbContext.OrderHeaders
                        .Include(u => u.OrderDetailsDto).Where(u => u.UserId == userId)
                        .OrderByDescending(u => u.OrderHeaderId)
                        .ToList();
                }
                _response.Result = _mapper.Map<IEnumerable<OrderHeaderDto>>(objectList);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [Authorize]
        [HttpGet("GetOrder/{id:int}")]
        public async Task<ResponseDto> Get(int id)
        {
            try
            {
                OrderHeader orderHeader = _appDbContext.OrderHeaders
                    .Include(u => u.OrderDetailsDto)
                    .First(u => u.OrderHeaderId == id);

                // Obtenemos todos los productos para poder cruzarlos
                var productos = await _productoService.GetProductos();

                // Asignar ProductDto a cada detalle
                foreach (var detail in orderHeader.OrderDetailsDto)
                {
                    detail.ProductDto = productos.FirstOrDefault(p => p.ProductId == detail.ProductId);
                }

                _response.Result = _mapper.Map<OrderHeaderDto>(orderHeader);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [Authorize]
        [HttpPost("CreateOrder")]
        public async Task<ResponseDto> CreateOrder([FromBody] CartDto cartDto)
        {
            try
            {
                OrderHeaderDto orderHeaderDto = _mapper.Map<OrderHeaderDto>(cartDto.CartHeaderDto);
                orderHeaderDto.OrderTime = DateTime.Now;
                orderHeaderDto.Status = Utilities.Status_Pending;

                var orderDetailsDto  = _mapper.Map<IEnumerable<OrderDetailsDto>>(cartDto.CartDetailsDtos);

                orderHeaderDto.OrderDetailsDto = orderDetailsDto;

                OrderHeader orderCreate = _appDbContext.OrderHeaders
                    .Add(_mapper.Map<OrderHeader>(orderHeaderDto)).Entity;

                await _appDbContext.SaveChangesAsync();

                //regresamos el valor de la entidad que se acaba de insertar
                orderHeaderDto.OrderHeaderId = orderCreate.OrderHeaderId;

                //retornamos la respuesta con la orden de compra
                _response.Result = orderHeaderDto;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
        #region Metodo de Pago en Línea
        [Authorize]
        [HttpPost("CreateStripeSession")]

        public async Task<ResponseDto> CreateStripeSession([FromBody] StripeRequestDto stripeRequestDto)
        {
            try
            {
                var options = new Stripe.Checkout.SessionCreateOptions
                {
                    SuccessUrl = stripeRequestDto.ApprovedUrl, //establecemos la url para consumir el servicio
                    CancelUrl = stripeRequestDto.CancelUrl, //establecemos la url de la cancelacion del servicio
                    //el LineItems representa una linea de los diferentes productos que se van a pagar
                    LineItems = new List<SessionLineItemOptions>(),
                    Mode = "payment",
                };

                //definimos el proceso de descuento con los cupones asignados de forma correcta
                var DiscountObj = new List<SessionDiscountOptions>
                {
                    new SessionDiscountOptions
                    {
                        Coupon = stripeRequestDto.OrderHeaderDto.CouponCode
                    }
                };

                //trabajamos con todos los productos que van a comprarse
                foreach(var item in stripeRequestDto.OrderHeaderDto.OrderDetailsDto)
                {
                    //por cada recorrido se crea un objeto del tipo sessionlineitemoptions
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.ProductDto.Name
                            }
                        },
                        Quantity = item.Count
                    };
                    options.LineItems.Add(sessionLineItem);
                }
                if (stripeRequestDto.OrderHeaderDto.Discount > 0)
                {
                    options.Discounts = DiscountObj;
                }
                var service = new Stripe.Checkout.SessionService();
                //al crear la sesion con stripe guardamos el objeto
                Stripe.Checkout.Session session = service.Create(options);
                //guardamos la url de la session en Stripe, esta url es la que se encarga de devolver la url de session Stripe
                stripeRequestDto.StripeSessionUrl = session.Url;
                //consultamos la sesion de order o el identificador de la orden en la bd
                OrderHeader orderHeader = _appDbContext.OrderHeaders.First(u => u.OrderHeaderId == stripeRequestDto.OrderHeaderDto.OrderHeaderId);
                orderHeader.StripeSessionId = session.Id;
                _response.Result = stripeRequestDto;
                   
            }catch(Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
        #endregion

        #region Confirmacion de Pago en Linea con Stripe
        [Authorize]
        [HttpPost("ValidateStripeSession")]
        public async Task<ResponseDto> ValidateStripeSession([FromBody] int orderHeaderId)
        {
            try
            {
                OrderHeader orderHeader = _appDbContext.OrderHeaders.First(u => u.OrderHeaderId == orderHeaderId);

                var service = new Stripe.Checkout.SessionService();
                //consultamos la sesion del service de stripe por el identificaador de la sesion
                Stripe.Checkout.Session session = service.Get(orderHeader.StripeSessionId);
                //ahora establecemos la intencion de pago en stripe
                var paymentIntentService = new PaymentIntentService();
                //consultamos el intento de pago a traves de la sesion de stripe
                PaymentIntent paymentIntent = paymentIntentService.Get(session.PaymentIntentId);

                if(paymentIntent.Status == "succeded")
                {
                    orderHeader.PaymentIntentId = paymentIntent.Id;
                    orderHeader.Status = Utilities.Status_Completed;
                    _appDbContext.SaveChanges();
                    //utilizamos la entidad para trabajar con el modulo de recompensas
                    RewardsDto rewardsDto = new()
                    {
                        OrderId = orderHeader.OrderHeaderId,
                        RewardsActivity = Convert.ToInt32(orderHeader.OrderTotal),
                        UserId = orderHeader.UserId,
                    };
                    //consultamos el nombre del topico
                    string topicName = _configuration.GetValue<string>("TopicAndQueueNmes: OrderCreatedTopic");
                    //enviamos el nombre del objeto y el nombre del topico
                    //await _messageBus.PublishMessage(rewardsDto, topicName);
                    //enviamos el nombre del objeto al exchange de RabbitMQ
                    //cuando mandas el mensaje por exchange llega a Rabbit pero no se puede leer hasta asignarlo
                    //_messageBus.SendMessage(rewardsDto, topicName);
                    _response.Result = _mapper.Map<OrderHeaderDto>(orderHeader);
                }
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [Authorize]
        [HttpPost("UpdateOrderStatus/{orderId:int}")]
        public async Task<ResponseDto> UpdateOrderStatus (int orderId, [FromBody] string newStatus)
        {
            try
            {
                OrderHeader orderHeader = _appDbContext.OrderHeaders.First(p => p.OrderHeaderId == orderId);
                if(orderHeader == null)
                {
                    if(newStatus == Utilities.Status_Cancelled)
                    {
                        var options = new RefundCreateOptions
                        {
                            Reason = RefundReasons.RequestedByCustomer,
                            PaymentIntent = orderHeader.PaymentIntentId
                        };
                        var service = new RefundService();
                        Refund refund = service.Create(options);
                    }
                    orderHeader.Status = newStatus;
                    _appDbContext.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
        #endregion
    }
}
