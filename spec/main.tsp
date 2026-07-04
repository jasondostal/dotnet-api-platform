import "@typespec/http";
import "@typespec/rest";
import "@typespec/openapi";
import "@typespec/versioning";

import "./errors.tsp";
import "./models.tsp";
import "./accounts.tsp";
import "./customers.tsp";
import "./workitems.tsp";
import "./insights.tsp";
import "./events.tsp";
import "./streaming.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;

/** ApiPlatform — design-first open-banking API reference. */
@service(#{
  title: "ApiPlatform",
})
@useAuth(PlatformOAuth2)
@versioned(Versions)
@server(
  "https://api.northwind.example",
  "Gateway (APIM) base; the API is mounted under /v1."
)
namespace ApiPlatform;

/** Supported API versions. */
enum Versions {
  /** Version 1 */
  v1: "v1",
}

/**
 * OAuth2 client-credentials for machine-to-machine and workforce consumers.
 * Scopes follow the platform `resource.action` convention.
 * All six platform scopes are declared here; operations reference specific subsets.
 */
model PlatformOAuth2
  is OAuth2Auth<
    [
      {
        type: OAuth2FlowType.clientCredentials;
        tokenUrl: "https://login.northwind.example/oauth2/token";
        scopes: [
          "account.read",
          "account.detailed.read",
          "transaction.read",
          "customer.read",
          "contact.read",
          "event.publish"
        ];
      }
    ]
  >;
