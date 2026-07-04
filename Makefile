# dotnet-api-platform — deploy the PoC (Container Apps), build + ship the API, tear down.
#
#   make spec            # compile TypeSpec → OpenAPI 3.1 in spec/tsp-output/
#   make gen             # alias for spec
#   make lint            # compile TypeSpec then spectral lint the emitted contract
#   make up              # create RG + deploy platform (ACR, Container Apps env, LA+AI, portal)
#   make deploy          # build image in ACR + create/update the app (blue/green revisions)
#   make portal          # build the dev portal + publish it to the Blob static website
#   make outputs         # show deployment outputs
#   make url             # print the live app URL
#   make down            # delete the whole resource group (~$0 at rest anyway)
#   make wire-events     # create the Event Grid webhook subscription → the live app
#   make events-demo     # fire one event; show the webhook log + queue fan-out

RG       ?= rg-apip-dev
LOC      ?= centralus
DEPLOY   := apip
TEMPLATE := infra/main.bicep
PARAMS   ?= infra/params/dev.bicepparam
APP      := apip-api
IMAGE    := apiplatform
WEBHOOK_SECRET ?= changeme-demo
DOTNET   := $(shell command -v dotnet 2>/dev/null || echo $(HOME)/.dotnet/dotnet)

.PHONY: help spec gen lint drift mock docs sanitize up whatif deploy portal outputs url down wire-events events-demo

help:
	@grep -E '^#   make' Makefile | sed 's/^#   /  /'

## ── TypeSpec source of truth ─────────────────────────────────────────────────
spec:        ## compile the TypeSpec spec → OpenAPI 3.1 + JSON Schema in spec/tsp-output/
	cd spec && npx tsp compile .

gen: spec    ## alias: compile TypeSpec (same as spec:)

## ── Design-first inner loop ─────────────────────────────────────────────────
lint: spec   ## compile TypeSpec then spectral lint the emitted OpenAPI contract
	npx spectral lint spec/tsp-output/openapi.v1.yaml --ruleset .spectral.yaml

drift: spec  ## boot the API and assert its runtime OpenAPI matches the emitted contract
	bash tools/drift-check.sh

mock:        ## run the Prism mock server (http://localhost:4010)
	npm run mock

docs:        ## build the static dev portal (multi-API catalog) into docs/portal/
	npm run docs:build

sanitize:    ## two-layer publish gate: structural patterns (committed) + local literal denylist (if present)
	@bash tools/sanitize.sh

## ── Platform infra ──────────────────────────────────────────────────────────
up:          ## create the RG and deploy the platform (ACR, env, observability, portal)
	az group create -n $(RG) -l $(LOC) -o none
	az deployment group create -g $(RG) -n $(DEPLOY) -f $(TEMPLATE) -p $(PARAMS) -o none
	@$(MAKE) --no-print-directory outputs

whatif:      ## preview the platform deployment diff
	az deployment group what-if -g $(RG) -n $(DEPLOY) -f $(TEMPLATE) -p $(PARAMS)

outputs:     ## print deployment outputs
	@az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs -o json 2>/dev/null

url:         ## print the live app URL
	@echo "https://$$(az containerapp show -g $(RG) -n $(APP) --query properties.configuration.ingress.fqdn -o tsv 2>/dev/null)"

down:        ## delete the whole resource group
	az group delete -n $(RG) --yes --no-wait

## ── Ship the API (blue/green via Container Apps revisions) ───────────────────
deploy:      ## build image in ACR, then create or blue/green-update the container app
	$(eval ACR := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.acrName.value -o tsv))
	$(eval LOGIN := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.acrLoginServer.value -o tsv))
	$(eval ENVN := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.containerEnvName.value -o tsv))
	$(eval AICONN := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.appInsightsConnectionString.value -o tsv))
	$(eval EGNAME := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.eventGridTopicName.value -o tsv))
	$(eval EGEP := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.eventGridEndpoint.value -o tsv))
	$(eval EGKEY := $(shell az eventgrid topic key list -g $(RG) -n $(EGNAME) --query key1 -o tsv))
	$(eval EVSA := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.eventsStorageAccount.value -o tsv))
	$(eval EVCONN := $(shell az storage account show-connection-string -g $(RG) -n $(EVSA) --query connectionString -o tsv))
	$(eval ENVARGS := "APPLICATIONINSIGHTS_CONNECTION_STRING=$(AICONN)" "EVENTGRID_TOPIC_ENDPOINT=$(EGEP)" "EVENTGRID_TOPIC_KEY=$(EGKEY)" "EVENTS_STORAGE_CONNECTION=$(EVCONN)" "EVENTS_QUEUE_NAMES=sink-a,sink-b" "WEBHOOK_SECRET=$(WEBHOOK_SECRET)")
	$(eval TAG := $(shell git rev-parse --short HEAD 2>/dev/null || date +%s))
	@echo ">> building $(LOGIN)/$(IMAGE):$(TAG) in ACR (cloud build, no local push)"
	az acr build -r $(ACR) -t $(IMAGE):$(TAG) . -o none
	@if az containerapp show -g $(RG) -n $(APP) >/dev/null 2>&1; then \
	  echo ">> blue/green: new revision r$(TAG) at 0% traffic"; \
	  az containerapp update -g $(RG) -n $(APP) --image $(LOGIN)/$(IMAGE):$(TAG) --revision-suffix r$(TAG) --set-env-vars $(ENVARGS) -o none; \
	  NEW=$$(az containerapp show -g $(RG) -n $(APP) --query properties.latestRevisionName -o tsv); \
	  echo ">> warming $$NEW, then cutting traffic"; sleep 10; \
	  az containerapp ingress traffic set -g $(RG) -n $(APP) --revision-weight $$NEW=100 -o none; \
	  echo ">> 100% traffic → $$NEW"; \
	else \
	  echo ">> first deploy: creating container app (scale-to-zero, ingress :8080)"; \
	  PW=$$(az acr credential show -n $(ACR) --query "passwords[0].value" -o tsv); \
	  az containerapp create -g $(RG) -n $(APP) --environment $(ENVN) \
	    --image $(LOGIN)/$(IMAGE):$(TAG) --target-port 8080 --ingress external \
	    --min-replicas 0 --max-replicas 2 --revisions-mode multiple \
	    --registry-server $(LOGIN) --registry-username $(ACR) --registry-password "$$PW" \
	    --env-vars $(ENVARGS) -o none; \
	fi
	@echo ">> URL: https://$$(az containerapp show -g $(RG) -n $(APP) --query properties.configuration.ingress.fqdn -o tsv)"

portal: docs ## enable the static website and upload the dev portal catalog
	$(eval SA := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.portalStorageAccount.value -o tsv))
	$(eval KEY := $(shell az storage account keys list -g $(RG) -n $(SA) --query "[0].value" -o tsv))
	az storage blob service-properties update --account-name $(SA) --account-key "$(KEY)" --static-website --index-document index.html -o none
	az storage blob upload-batch -s docs/portal -d '$$web' --account-name $(SA) --account-key "$(KEY)" --overwrite -o none
	@echo ">> portal: $$(az storage account show -n $(SA) -g $(RG) --query primaryEndpoints.web -o tsv)"

## ── Eventing (Event Grid: webhook + queue fan-out) ──────────────────────────
wire-events: ## create the Event Grid webhook subscription → the live app /hooks/events
	$(eval EGNAME := $(shell az deployment group show -g $(RG) -n $(DEPLOY) --query properties.outputs.eventGridTopicName.value -o tsv))
	$(eval TOPICID := $(shell az eventgrid topic show -g $(RG) -n $(EGNAME) --query id -o tsv))
	$(eval FQDN := $(shell az containerapp show -g $(RG) -n $(APP) --query properties.configuration.ingress.fqdn -o tsv))
	az eventgrid event-subscription create --name to-webhook \
	  --source-resource-id $(TOPICID) \
	  --endpoint "https://$(FQDN)/hooks/events?key=$(WEBHOOK_SECRET)" \
	  --endpoint-type webhook --event-delivery-schema cloudeventschemav1_0 -o none
	@echo ">> webhook wired → https://$(FQDN)/hooks/events"

events-demo: ## fire one event and show the webhook log + queue fan-out
	$(eval FQDN := $(shell az containerapp show -g $(RG) -n $(APP) --query properties.configuration.ingress.fqdn -o tsv))
	@echo ">> POST /touch — publish one event"
	@curl -fsS -X POST -H "X-Scopes: event.publish" "https://$(FQDN)/v1/accounts/7c9e6679-7425-40de-944b-e07fc1f90ae7/touch"; echo
	@echo ">> waiting for fan-out…"; sleep 8
	@echo ">> webhook received:"; curl -fsS "https://$(FQDN)/hooks/events/log"; echo
	@echo ">> queue fan-out:";    curl -fsS "https://$(FQDN)/hooks/queues"; echo
