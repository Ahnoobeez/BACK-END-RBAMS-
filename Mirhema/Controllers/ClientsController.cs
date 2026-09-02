using Microsoft.AspNetCore.Mvc;
using Mirhema.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace Mirhema.Controllers
{
    public class ClientsController : Controller
    {
        private string baseUrl = "http://localhost:5073/";


        public async Task<IActionResult> Index()
        {
            List<ClientEntity> lstClients = new List<ClientEntity>();

            using (var _httpClient = new HttpClient())
            {
                _httpClient.BaseAddress = new Uri(baseUrl + "api/Clients/");
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage getData = await _httpClient.GetAsync("");

                if (getData.IsSuccessStatusCode)
                {
                    string results = getData.Content.ReadAsStringAsync().Result;
                    lstClients = JsonConvert.DeserializeObject<List<ClientEntity>>(results);
                }
                else
                {
                    return View("ErrorPage");
                }
            }

            return View(lstClients);
        }

        public async Task<IActionResult> Create() 
        { 
            return View();
        }

        public async Task<IActionResult> CreateClient(ClientEntity clientEntity) 
        {
            using (var _httpClient = new HttpClient())
            {
                _httpClient.BaseAddress = new Uri(baseUrl + "api/Clients/");
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage getData = await _httpClient.PostAsJsonAsync("", clientEntity);

                if (getData.IsSuccessStatusCode)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    return View("ErrorPage");
                }
            }
        }


        public IActionResult ErrorPage() 
        {
            return View();
        } 

    }
}
