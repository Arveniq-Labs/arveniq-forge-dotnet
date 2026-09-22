using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Arveniq.Forge.Widget;

/// <summary>Server-rendered dark agent drawer styled after the Forge/PingLead assistant design.</summary>
public sealed class ForgeChatWidget
{
    private readonly JsonObject _context;

    public ForgeChatWidget(ForgeChatWidgetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Id = string.IsNullOrWhiteSpace(options.Id) ? "forge-widget-" + Guid.NewGuid().ToString("N") : ValidateId(options.Id);
        Title = Fallback(options.Title, "PingLead AI");
        AgentName = Fallback(options.AgentName, "Web Research Agent");
        AgentSubtitle = Fallback(options.AgentSubtitle, "is ready to help with your work.");
        ContextLabel = Fallback(options.ContextLabel, "this page");
        Endpoint = Fallback(options.Endpoint, "/api/forge/chat");
        Placeholder = Fallback(options.Placeholder, $"Message {AgentName}...");
        LauncherLabel = Fallback(options.LauncherLabel, $"Ask {Title}");
        AgentsAvailable = Math.Max(0, options.AgentsAvailable);
        Shortcut = options.Shortcut ?? WidgetShortcut.MetaKey("K");
        _context = (options.Context ?? new JsonObject()).DeepClone() as JsonObject ?? new JsonObject();
        QuickPrompts = options.QuickPrompts ?? ["Summarize this customer or lead.", "Draft a thoughtful follow-up.", "Recommend the next best action."];
    }

    public string Id { get; }
    public string Title { get; }
    public string AgentName { get; }
    public string AgentSubtitle { get; }
    public string ContextLabel { get; }
    public string Endpoint { get; }
    public string Placeholder { get; }
    public string LauncherLabel { get; }
    public int AgentsAvailable { get; }
    public WidgetShortcut Shortcut { get; }
    public IReadOnlyList<string> QuickPrompts { get; }

    /// <summary>Returns an isolated HTML/CSS/JS fragment for Razor, MVC, or any server-side view.</summary>
    public string Render()
    {
        var configuration = new JsonObject { ["endpoint"] = Endpoint, ["context"] = _context.DeepClone(), ["contextLabel"] = ContextLabel, ["shortcut"] = Shortcut.ToJson() };
        var prompts = string.Concat(QuickPrompts.Select(prompt => $"<button type=\"button\" data-forge-prompt=\"{Html(prompt)}\">{Html(prompt)}</button>"));
        return $"""<section id="{Html(Id)}" class="forge-widget" aria-label="{Html(Title)}" hidden><style>{Styles.Replace("$ROOT", "#" + Id, StringComparison.Ordinal)}</style><div class="forge-backdrop" data-forge-close></div><aside class="forge-drawer" role="dialog" aria-modal="true" aria-label="{Html(Title)}"><header class="forge-header"><div class="forge-heading"><span class="forge-mark" aria-hidden="true">✧</span><div><strong>{Html(Title)}</strong><small>◉ {AgentsAvailable} agents available</small></div></div><button class="forge-close" type="button" data-forge-close aria-label="Close assistant">×</button></header><div class="forge-agent"><span>{Html(AgentName)}</span><span aria-hidden="true">⌄</span></div><div class="forge-context">Using context <strong>{Html(ContextLabel)}</strong></div><main class="forge-main"><span class="forge-hero-mark" aria-hidden="true">✧</span><p class="forge-eyebrow">ASK {Html(Title).ToUpperInvariant()}</p><h2>Work with your {Html(ContextLabel)} data</h2><p class="forge-subtitle">{Html(AgentName)} {Html(AgentSubtitle)}</p><div class="forge-prompts">{prompts}</div><div class="forge-transcript" aria-live="polite"></div></main><form class="forge-composer"><input aria-label="Message {Html(AgentName)}" placeholder="{Html(Placeholder)}" autocomplete="off"/><button type="submit" aria-label="Send message">↗</button></form></aside><script type="application/json" data-forge-config>{configuration.ToJsonString(JsonHelpers.SerializerOptions)}</script><script>{Script.Replace("$ID", JsonSerializer.Serialize(Id), StringComparison.Ordinal)}</script></section>""";
    }

    /// <summary>A compact host-page opener matching the screenshot's Ask action.</summary>
    public string RenderLauncher() => $"<button type=\"button\" data-forge-open=\"{Html(Id)}\" style=\"border:1px solid #314b78;border-radius:8px;background:#1d3b69;color:#b9d3ff;padding:9px 14px;font:600 13px system-ui;cursor:pointer\">✧ {Html(LauncherLabel)} <kbd style=\"margin-left:8px;color:#92aed9;font-size:11px\">{Html(Shortcut.Display)}</kbd></button>";

    private static string Html(string value) => WebUtility.HtmlEncode(value) ?? string.Empty;
    private static string Fallback(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string ValidateId(string value) => System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9_-]{0,79}$") ? value : throw new ArgumentException("Widget id must start with a letter and contain only letters, digits, _ or -.", nameof(value));

    private const string Styles = """
    $ROOT{position:fixed;inset:0;z-index:2147483000;font-family:Inter,ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;color:#eef2ff;letter-spacing:-.01em}$ROOT[hidden]{display:none}$ROOT *{box-sizing:border-box}$ROOT .forge-backdrop{position:absolute;inset:0;background:rgba(2,6,13,.46)}$ROOT .forge-drawer{position:absolute;top:0;right:0;display:flex;flex-direction:column;width:min(460px,100vw);height:100%;background:#101722;border-left:1px solid #273246;box-shadow:-24px 0 60px rgba(0,0,0,.35)}$ROOT .forge-header{display:flex;align-items:center;justify-content:space-between;padding:15px 16px 12px}$ROOT .forge-heading{display:flex;gap:10px;align-items:center}$ROOT .forge-mark,$ROOT .forge-hero-mark{display:grid;place-items:center;background:#312550;border:1px solid #715ab2;color:#d5c5ff;border-radius:10px;font-size:24px;width:38px;height:38px;line-height:1}$ROOT .forge-heading strong{display:block;font-size:16px;line-height:18px}$ROOT .forge-heading small{display:block;color:#aab8ca;font-size:12px;font-weight:600;margin-top:3px}$ROOT .forge-close{border:0;background:transparent;color:#abb9cc;font-size:29px;line-height:24px;cursor:pointer;padding:3px 0 6px 12px}$ROOT .forge-close:hover{color:white}$ROOT .forge-agent{margin:0 16px;border:1px solid #2c3a4e;background:#111b29;border-radius:8px;padding:12px;display:flex;justify-content:space-between;color:#b8c8de;font-size:13px;font-weight:650}$ROOT .forge-context{margin:22px 16px 0;padding:0 0 12px;border-bottom:1px solid #253145;color:#aebcd0;font-size:12px}$ROOT .forge-context strong{margin-left:7px;color:#edf1f8}$ROOT .forge-main{padding:32px 22px 18px;overflow:auto;flex:1}$ROOT .forge-hero-mark{width:44px;height:44px;font-size:27px;margin:0 0 14px 6px}$ROOT .forge-eyebrow{font-size:11px;font-weight:800;letter-spacing:.11em;color:#aabfea;margin:0 0 8px 6px}$ROOT h2{font-size:18px;line-height:24px;margin:0 6px 8px;font-weight:560}$ROOT .forge-subtitle{font-size:14px;line-height:20px;color:#adbad0;margin:0 6px 16px}$ROOT .forge-prompts{display:grid;grid-template-columns:1fr 1fr;gap:8px 10px;margin:0 6px}$ROOT .forge-prompts button{min-height:55px;text-align:left;padding:11px;border:1px solid #2a374a;border-radius:8px;background:transparent;color:#ebeff7;font:inherit;font-size:13px;line-height:17px;cursor:pointer}$ROOT .forge-prompts button:hover{border-color:#7660b6;background:#171e2e}$ROOT .forge-transcript{display:grid;gap:10px;padding:20px 6px 6px}$ROOT .forge-answer{padding:12px 13px;border-radius:9px;background:#192235;border:1px solid #2c3b52;color:#dfe7f4;font-size:13px;line-height:19px;white-space:pre-wrap}$ROOT .forge-status{color:#9dacc3;font-size:12px;padding:0 6px}$ROOT .forge-composer{display:flex;gap:8px;align-items:center;margin:0 16px 9px;padding:4px 5px 4px 14px;min-height:52px;border:1px solid #3a4658;background:#111925;border-radius:14px}$ROOT .forge-composer input{min-width:0;flex:1;border:0;outline:0;background:transparent;color:#eff4fb;font:inherit;font-size:13px}$ROOT .forge-composer input::placeholder{color:#738399}$ROOT .forge-composer button{width:39px;height:39px;border:0;border-radius:10px;background:#76659f;color:#d9d1ee;font-size:21px;cursor:pointer}$ROOT .forge-composer button:disabled{opacity:.55;cursor:wait}@media(max-width:520px){$ROOT .forge-drawer{width:100%}$ROOT .forge-backdrop{display:none}}
    """;
    private const string Script = """
    (function(){const root=document.getElementById($ID);if(!root)return;const config=JSON.parse(root.querySelector('[data-forge-config]').textContent);const input=root.querySelector('input'),form=root.querySelector('form'),send=form.querySelector('button'),transcript=root.querySelector('.forge-transcript');const open=()=>{root.hidden=false;setTimeout(()=>input.focus(),0)},close=()=>{root.hidden=true};root.querySelectorAll('[data-forge-close]').forEach(button=>button.addEventListener('click',close));document.addEventListener('click',event=>{const trigger=event.target instanceof Element?event.target.closest('[data-forge-open]'):null;if(trigger&&trigger.dataset.forgeOpen===root.id)open()});document.addEventListener('keydown',event=>{const key=event.key===' '?'SPACE':String(event.key).toUpperCase();const s=config.shortcut;if(key===s.key&&event.metaKey===s.meta&&event.ctrlKey===s.ctrl&&event.altKey===s.alt&&event.shiftKey===s.shift){event.preventDefault();open()}});root.querySelectorAll('[data-forge-prompt]').forEach(button=>button.addEventListener('click',()=>{input.value=button.dataset.forgePrompt;form.requestSubmit()}));function status(text){const node=document.createElement('p');node.className='forge-status';node.textContent=text;transcript.append(node);return node}function id(){return globalThis.crypto&&crypto.randomUUID?crypto.randomUUID():Date.now()+'-'+Math.random().toString(36).slice(2)}function render(raw,answer){if(raw.type==='response.delta')answer.textContent+=(raw.data&&raw.data.delta)||'';if(raw.type==='response.completed'&&raw.data&&raw.data.text)answer.textContent=raw.data.text}async function consume(response,answer){if(!response.body){answer.textContent='The assistant did not return a response.';return}const reader=response.body.getReader(),decoder=new TextDecoder();let buffer='';for(;;){const part=await reader.read();if(part.done)break;buffer+=decoder.decode(part.value,{stream:true});const frames=buffer.split(/\r?\n\r?\n/);buffer=frames.pop();for(const frame of frames){const data=frame.split(/\r?\n/).filter(line=>line.startsWith('data:')).map(line=>line.slice(5).replace(/^ /,'')).join('\n');if(!data)continue;try{render(JSON.parse(data),answer)}catch(_){}}}}form.addEventListener('submit',async event=>{event.preventDefault();const message=input.value.trim();if(!message)return;input.value='';send.disabled=true;const progress=status('Working with '+config.contextLabel+'…');const answer=document.createElement('div');answer.className='forge-answer';transcript.append(answer);try{const response=await fetch(config.endpoint,{method:'POST',credentials:'same-origin',headers:{'content-type':'application/json','accept':'text/event-stream'},body:JSON.stringify({message,clientMessageId:id(),context:config.context})});if(!response.ok)throw new Error('Unable to start assistant');await consume(response,answer);if(!answer.textContent)answer.textContent='No answer was returned.'}catch(error){answer.textContent='Unable to reach the assistant. Please try again.'}finally{progress.remove();send.disabled=false;input.focus()}});})();
    """;
}

public sealed class ForgeChatWidgetOptions
{
    public string? Id { get; init; }
    public string? Title { get; init; } = "PingLead AI";
    public string? AgentName { get; init; } = "Web Research Agent";
    public string? AgentSubtitle { get; init; } = "is ready to help with your work.";
    public int AgentsAvailable { get; init; } = 7;
    public string? ContextLabel { get; init; } = "this page";
    /// <summary>Sent with the turn for UI convenience only. Resolve authoritative data in the backend.</summary>
    public JsonObject? Context { get; init; }
    public string? Endpoint { get; init; } = "/api/forge/chat";
    public WidgetShortcut? Shortcut { get; init; } = WidgetShortcut.MetaKey("K");
    public string? Placeholder { get; init; }
    public string? LauncherLabel { get; init; }
    public IReadOnlyList<string>? QuickPrompts { get; init; }
}
