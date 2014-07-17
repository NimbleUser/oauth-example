using DotNetOpenAuth.OAuth2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

namespace OAuthExample
{
	class SelfServiceClient : WebServerClient
	{
		// read the base Self Service URL from the Web.config
		// this value is provided by NimbleUser for each Self Service site
		private static readonly Uri baseUrl = new Uri(ConfigurationManager.AppSettings["SelfServiceBaseUrl"]);
		private static readonly Uri authorizationEndpoint = new Uri(baseUrl, "oauth/authorize");
		private static readonly Uri tokenEndpoint = new Uri(baseUrl, "oauth/token");
		private static readonly Uri userInfoEndpoint = new Uri(baseUrl, "oauth/user");

		private static readonly AuthorizationServerDescription SelfServiceDescription = new AuthorizationServerDescription
		{
			AuthorizationEndpoint = authorizationEndpoint,
			TokenEndpoint = tokenEndpoint,
		};

		public SelfServiceClient() : base(SelfServiceDescription) { }

		// makes a request to the userinfo endpoint using the OAuth authorization token
		// returns some standard claims plus custom ones depending on the implementation
		// these include: sub (unique identifier in Nimble AMS), given_name, family_name,
		// email, preferred_username, locale, language, company_name
		// http://openid.net/specs/openid-connect-basic-1_0.html#StandardClaims
		public JObject GetUserInfo(IAuthorizationState authorizationState)
		{
			var request = (HttpWebRequest)HttpWebRequest.Create(userInfoEndpoint);
			AuthorizeRequest(request, authorizationState);
			try
			{
				var response = request.GetResponse();
				{
					using (var streamReader = new StreamReader(response.GetResponseStream()))
					using (var jsonReader = new JsonTextReader(streamReader))
					{
						return JObject.Load(jsonReader);
					}
				}
			}
			// TODO: error handling
			catch (WebException e)
			{
				using (StreamReader reader = new StreamReader(e.Response.GetResponseStream()))
				{
					throw new Exception(reader.ReadToEnd(), e);
				}
			}
		}
	}

	public partial class Default : System.Web.UI.Page
	{
		private static readonly SelfServiceClient client = new SelfServiceClient()
		{
			// read the client identifier and client secret from the Web.config
			// these values are provided by NimbleUser for each Self Service site
			ClientIdentifier = ConfigurationManager.AppSettings["ClientIdentifier"],
			ClientCredentialApplicator = ClientCredentialApplicator.PostParameter(ConfigurationManager.AppSettings["ClientSecret"])
		};

		protected void Page_Load(object sender, EventArgs e)
		{
			IAuthorizationState authorization = client.ProcessUserAuthorization();

			// start the authorization process if we haven't already
			if (authorization == null)
			{
				// this will redirect to the Self Service site to start the authorization process
				// the openid scope is required in order for access to be granted to the userinfo endpoint
				// an alternate return URL can be specified if desired
				client.RequestUserAuthorization(new string[] { "openid" } /*, returnTo */);
			}
			else
			{
				// authorization process is complete, now we can hit the userinfo endpoint
				var userInfo = client.GetUserInfo(authorization);

				// debug output of the data returned by the userinfo endpoint
				LtResult.Text = string.Join("<br/>", userInfo.Properties().Select(x => HttpUtility.HtmlEncode(x.Name + " - " + x.Value.ToString())));
			}
		}
	}
}