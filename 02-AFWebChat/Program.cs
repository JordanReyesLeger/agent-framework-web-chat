using AFWebChat.Agents;
using AFWebChat.Agents.A2A;
using AFWebChat.Agents.Basic;
using AFWebChat.Agents.Composite;
using AFWebChat.Agents.ContextAware;
using AFWebChat.Agents.Domain;
using AFWebChat.Agents.Enterprise;
using AFWebChat.Agents.HumanResources;
using AFWebChat.Agents.Mcp;
using AFWebChat.Agents.Multimodal;
using AFWebChat.Agents.StructuredOutput;
using AFWebChat.Agents.Tools;
using AFWebChat.Agents.Workflow;
using AFWebChat.Bot;
using AFWebChat.ContextProviders;
using AFWebChat.Models;
using AFWebChat.Orchestrations;
using AFWebChat.Services;
using AFWebChat.Tools;
using AFWebChat.Tools.Plugins;
using AFWebChat.Workflows;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add MVC + Controllers
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Branding
builder.Services.Configure<AppBrandingSettings>(builder.Configuration.GetSection("AppBranding"));

// Register services
builder.Services.AddSingleton<ReasoningSettings>();
builder.Services.AddSingleton<ChatClientFactory>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<AgentRegistry>();
builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddSingleton<WorkflowFactory>();
builder.Services.AddSingleton<OrchestrationFactory>();
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractorService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IDocumentIndexingService, DocumentIndexingService>();
builder.Services.AddSingleton<VoiceConversationService>();
builder.Services.AddScoped<AgentOrchestrationService>();

// Register plugins
builder.Services.AddSingleton<SqlPlugin>();
builder.Services.AddSingleton<GetSchemaPlugin>();
builder.Services.AddSingleton<QuerySqlPlugin>();
// builder.Services.AddSingleton<INEGICensoPlugin>();   // Removed — INEGI plugins not in this project
// builder.Services.AddSingleton<INEGIOntologyPlugin>(); // Removed — INEGI plugins not in this project
builder.Services.AddSingleton<LegalIndexPlugin>();
builder.Services.AddSingleton<SkillIndexPlugin>();
builder.Services.AddSingleton<WebScrapingPlugin>();
builder.Services.AddSingleton<BingGroundingPlugin>();
builder.Services.AddSingleton<McpServerPlugin>();
// builder.Services.AddSingleton<AMXDatabasePlugin>();  // TODO: create these plugins
// builder.Services.AddSingleton<AMXOntologyPlugin>();
builder.Services.AddSingleton<AzureSearchPlugin>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<AzureSearchPlugin>>();
    var endpoint = config["AzureSearch:Endpoint"];
    var indexName = config["AzureSearch:IndexName"] ?? "skill";
    var apiKey = config["AzureSearch:ApiKey"];

    if (string.IsNullOrEmpty(endpoint))
    {
        logger.LogWarning("AzureSearch:Endpoint not configured. AzureSearchPlugin will not be functional.");
        return new AzureSearchPlugin(
            new Azure.Search.Documents.SearchClient(
                new Uri("https://placeholder.search.windows.net"), "placeholder",
                new Azure.AzureKeyCredential("placeholder")),
            logger, config);
    }

    Azure.Search.Documents.SearchClient searchClient;
    if (!string.IsNullOrEmpty(apiKey))
    {
        searchClient = new Azure.Search.Documents.SearchClient(
            new Uri(endpoint), indexName, new Azure.AzureKeyCredential(apiKey));
        logger.LogInformation("AzureSearchPlugin: Using API Key auth for index '{IndexName}'", indexName);
    }
    else
    {
        searchClient = new Azure.Search.Documents.SearchClient(
            new Uri(endpoint), indexName, new Azure.Identity.DefaultAzureCredential());
        logger.LogInformation("AzureSearchPlugin: Using DefaultAzureCredential for index '{IndexName}'", indexName);
    }
    return new AzureSearchPlugin(searchClient, logger, config);
});

// Register context providers
builder.Services.AddSingleton<AzureSearchRAGProvider>();

// ---- A2A (Agent2Agent) protocol ----
var a2aSettings = A2AIntegration.ReadSettings(builder.Configuration);
builder.Services.AddSingleton(a2aSettings);
builder.Services.AddSingleton<A2ADirectory>();

// El hosting A2A resuelve el store de sesiones por clave = nombre del agente. Con AnyKey cada
// agente publicado obtiene su propio store y las conversaciones A2A conservan el historial.
builder.Services.AddKeyedSingleton<Microsoft.Agents.AI.Hosting.AgentSessionStore, InMemoryAgentSessionStore>(
    KeyedService.AnyKey);

// ---- Bot Framework (Teams / WebChat channel) ----
builder.Services.AddHttpClient();

// Los agentes A2A remotos pueden razonar durante minutos; el timeout por defecto (100s) no alcanza.
builder.Services.AddHttpClient(A2ARemoteAgent.HttpClientName,
    client => client.Timeout = TimeSpan.FromSeconds(a2aSettings.RemoteTimeoutSeconds));
builder.AddAgentApplicationOptions();
builder.AddAgent<TeamsBotAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddSingleton<ConversationReferenceStore>();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

var app = builder.Build();

// Register all agent definitions
var registry = app.Services.GetRequiredService<AgentRegistry>();

// ── Core agents ──
registry.Register(GeneralAssistantAgent.CreateDefinition());
// Summarizer = único agente que permanece en Chat Completions (demo). Translator usa Responses.
registry.Register(SummarizerAgent.CreateDefinition());
registry.Register(TranslatorAgent.CreateDefinition());

// ── Tool agents ──
registry.Register(DatabaseQueryAgent.CreateDefinition());
registry.Register(WebSearchAgent.CreateDefinition());
registry.Register(LightsAgent.CreateDefinition());

// ── Structured output agents ──
registry.Register(EntityExtractorAgent.CreateDefinition());
registry.Register(SentimentAnalyzerAgent.CreateDefinition());

// ── Multimodal agents ──
registry.Register(VisionAgent.CreateDefinition());

// ── Composite agents ──
registry.Register(ResearchAssistantAgent.CreateDefinition());

// ── Context-aware agents ──
registry.Register(MemoryAgent.CreateDefinition());
registry.Register(AzureSearchAgent.CreateDefinition());

// ── MCP agents ──
registry.Register(McpToolsAgent.CreateDefinition());

// ── Domain agents ──
registry.Register(SqlAzureAgent.CreateDefinition());
registry.Register(BingGroundingAgent.CreateDefinition());

// ── Foundry agents ──
registry.Register(FoundrySimpleBotAgent.CreateDefinition());
registry.Register(FoundryOrchestratorAgent.CreateDefinition());

// ── Enterprise agents ──
registry.Register(MultiAgentPlannerAgent.CreateDefinition());
registry.Register(DataStorytellerAgent.CreateDefinition());

// ── Human Resources agents ──
registry.Register(EvaluadorDeCVAgent.CreateDefinition());

// ── Workflow agents (used internally by orchestrations, not listed in sidebar) ──
// Plan de Proyecto
registry.Register(AnalistaDeNegocioAgent.CreateDefinition());
registry.Register(EstimadorDeCostosAgent.CreateDefinition());
registry.Register(PlanificadorDeProyectoAgent.CreateDefinition());
// Equipo Desarrollo (GroupChat)
registry.Register(DesarrolladorAgent.CreateDefinition());
registry.Register(ArquitectoAgent.CreateDefinition());
registry.Register(ProjectManagerAgent.CreateDefinition());
registry.Register(DBAAgent.CreateDefinition());
// Gestión de Correos (GroupChat AI)
registry.Register(EvaluadorDeUrgenciaAgent.CreateDefinition());
registry.Register(BuscadorDeCorreosAgent.CreateDefinition());
registry.Register(RedactorDeRespuestaAgent.CreateDefinition());

// ── A2A remote agents (otro framework/lenguaje hablando el protocolo A2A) ──
var a2aDirectory = app.Services.GetRequiredService<A2ADirectory>();
var a2aLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AFWebChat.A2A");
a2aDirectory.Settings = a2aSettings;
a2aDirectory.RemoteAgents = A2AIntegration.RegisterRemoteAgents(registry, a2aSettings, a2aLogger);

// Warm up Azure credential and OpenAI client at startup
var chatFactory = app.Services.GetRequiredService<ChatClientFactory>();
_ = Task.Run(async () => await chatFactory.WarmUpAsync());

// Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseWebSockets();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Bot Framework endpoint: /api/messages
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

// A2A endpoints: publica los agentes locales configurados en /a2a/{Agente}
a2aDirectory.ExposedAgents = A2AIntegration.MapExposedAgents(app, a2aSettings);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
