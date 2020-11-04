using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Omnia.CLI.Commands.Model.Behaviours.Data;
using Omnia.CLI.Infrastructure;

namespace Omnia.CLI.Commands.Model.Behaviours
{
	public class DefinitionService
	{
		private readonly IApiClient _apiClient;

		public DefinitionService(IApiClient apiClient)
		{
			_apiClient = apiClient;
		}

		public async Task<bool> ReplaceApplicationBehaviourData(string tenant, string environment,
			string entity,
			ApplicationBehaviour applicationBehaviourData)
		{
			var patch = new JsonPatchDocument();

			patch.Replace("/expression", applicationBehaviourData.Expression);

			if (applicationBehaviourData.Usings?.Count > 0)
				patch.Replace("/behaviourNamespaces",
					applicationBehaviourData.Usings.Select(u => MapToBehaviourNamespace(u, applicationBehaviourData.Namespace)));

			var dataAsString = JsonConvert.SerializeObject(patch, new Newtonsoft.Json.Converters.StringEnumConverter());

			var response = await _apiClient.Patch($"/api/v1/{tenant}/{environment}/model/ApplicationBehaviour/{entity}",
				new StringContent(dataAsString,
								Encoding.UTF8,
								"application/json")).ConfigureAwait(false);
			return response.Success;
		}

		public async Task<bool> ReplaceStateData(string tenant, string environment,
			string entity,
			List<State> states)
		{
			var metadata = await GetMetadataForStateMachine(tenant, environment, entity).ConfigureAwait(false);

			var patch = new JsonPatchDocument();

			if (states.Count > 0)
				patch = PatchStatesToReplace(patch, metadata, states);

			var dataAsString = JsonConvert.SerializeObject(patch, new Newtonsoft.Json.Converters.StringEnumConverter());

			var response = await _apiClient.Patch($"/api/v1/{tenant}/{environment}/model/StateMachine/{entity}",
				new StringContent(dataAsString,
								Encoding.UTF8,
								"application/json")).ConfigureAwait(false);
			return response.Success;
		}

		public async Task<bool> ReplaceData(string tenant, string environment,
			string entity,
			Entity entityData)
		{
			var definition = await GetDefinitionForEntity(tenant, environment, entity).ConfigureAwait(false);

			var patch = new JsonPatchDocument();
			if (entityData.EntityBehaviours?.Count > 0)
				patch.Replace("/entityBehaviours", entityData.EntityBehaviours.ToArray());
			if (entityData.DataBehaviours?.Count > 0)
				patch.Replace("/dataBehaviours", entityData.DataBehaviours.ToArray());
			if (entityData.Usings?.Count > 0)
				patch.Replace("/behaviourNamespaces",
					entityData.Usings.Select(u => MapToBehaviourNamespace(u, entityData.Namespace)));

			var dataAsString = JsonConvert.SerializeObject(patch, new Newtonsoft.Json.Converters.StringEnumConverter());

			var response = await _apiClient.Patch($"/api/v1/{tenant}/{environment}/model/{definition}/{entity}",
				new StringContent(dataAsString,
								Encoding.UTF8,
								"application/json")).ConfigureAwait(false);
			return response.Success;
		}

		private async Task<string> GetDefinitionForEntity(string tenant, string environment, string entity)
		{
			var (details, content) = await _apiClient.Get($"/api/v1/{tenant}/{environment}/model/output/definitions/{entity}")
				.ConfigureAwait(false);

			if (!details.Success) return null;

			var data = ((JObject)JsonConvert.DeserializeObject(content));

			if (data.TryGetValue("instanceOf", out var definition))
				return definition.ToString();
			return null;
		}

		private async Task<JObject> GetMetadataForStateMachine(string tenant, string environment, string entity)
		{
			var (details, content) = await _apiClient.Get($"/api/v1/{tenant}/{environment}/model/StateMachine/{entity}")
				.ConfigureAwait(false);

			if (!details.Success) return null;

			var data = ((JObject)JsonConvert.DeserializeObject(content));

			if (data.TryGetValue("name", out var definition) && definition.Value<string>().Equals(entity))
				return data;
			return null;
		}

		private JsonPatchDocument PatchStatesToReplace(JsonPatchDocument patch, JObject metadata, List<State> states)
		{
			if (metadata.TryGetValue("states", out var metadataStates))
			{
				for (int sn = 0; sn < metadataStates.Count(); sn++)
				{
					var state = states.Where(s => s.Name.Equals(metadataStates[sn]["name"].Value<string>())).FirstOrDefault();
					if (state != null)
					{
						patch.Replace($"/states/{sn}/assignToExpression", state.AssignToExpression);

						if (state.Behaviours.Count > 0)
						{
							var behaviours = metadataStates[sn]["behaviours"];
							patch.Replace($"/states/{sn}/behaviours", state.Behaviours.ToArray());
						}

						if(state.Transitions.Count > 0)
						{
							var transitions = metadataStates[sn]["transitions"];
							for (int tn = 0; tn < transitions.Count(); tn++)
							{
								var transition = state.Transitions.Where(t => t.Name.Equals(transitions[tn]["name"].Value<string>())).FirstOrDefault();
								if (transition != null)
									patch.Replace($"/states/{sn}/transitions/{tn}/expression", transition.Expression);
							}
						}
					}
				}
				return patch;
			}
			return null;
		}

		private static object MapToBehaviourNamespace(string usingDirective, string @namespace)
		=> new
		{
			Name = usingDirective.Replace(".", ""),
			ExecutionLocation = @namespace.Split('.').ElementAtOrDefault(3) ?? "Internal",
			FullyQualifiedName = usingDirective
		};
	}
}