import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { renderToStaticMarkup } from 'react-dom/server';
import { createElement } from 'react';
import { validate, text, callback, utc, addressable } from './input.mjs';
import { observe, created, reason } from './observation.mjs';
import { EditorController } from './controller.mjs';
import { editorClient } from './client.mjs';
import { bindLifecycle } from './lifecycle.mjs';
import { focusList } from "./focus.mjs";
import { CreateForm, CreateFields, DetailsView, ProblemView } from './views.mjs';
const now=Date.parse('2026-11-01T12:00:00Z');
const draft={clientId:'example-web',displayName:'Example',redirectUris:'https://client.example/callback',credentialExpiresAt:'2030-01-01T00:00:00Z'};
const input=validate(draft,now).input;
const details={id:'opaque-id',clientId:input.clientId,displayName:input.displayName,redirectUris:input.redirectUris,profile:'managed-web-v1',editable:true,credential:{issuedAt:'2026-11-01T12:00:00Z',expiresAt:input.credentialExpiresAt},expiresAt:'2026-11-01T12:01:00Z'};
const tag=id=>`"${createHash('sha256').update(id).digest('hex')}-1"`;
const secret=Buffer.alloc(32,91).toString('base64');
const read={status:200,value:details,etag:tag(details.id)};
const success={status:201,value:{application:details,clientSecret:secret},etag:read.etag};
const session={kind:'authenticated',subject:'synthetic-subject',expires:now+120000};
const proof={status:200,value:{requestToken:'native_request-proof'}};
const deferred=()=>{let resolve,reject;const promise=new Promise((a,b)=>{resolve=a;reject=b});return {promise,resolve,reject}};
function setup(t, overrides={}, clock=()=>now) {
 const states=[],calls={token:0,create:0,details:0};const client={
 token:async()=>{calls.token++;return proof},create:async()=>{calls.create++;return success},details:async()=>{calls.details++;return read},...overrides};
 const owner=new EditorController(client,state=>states.push(state),clock);owner.authorize(session);t.after(()=>owner.dispose());
 return {owner,states,calls};
}
const headers={'content-type':'application/json','cache-control':'no-store','x-content-type-options':'nosniff','etag':read.etag};

test('Given_explicit_creation_fields_When_validated_Then_exact_values_and_UTC_are_preserved',()=>{
 assert.deepEqual(validate(draft,now),{input,errors:{}});
 assert.equal(validate({...draft,displayName:' Name 😀 ',redirectUris:draft.redirectUris+'\nhttps://client.example/other'},now).input.displayName,' Name 😀 ');
 for(const [field,values] of Object.entries({clientId:['',' ','a b','a\u0085b','x'.repeat(101),'x\u0000','\ud800'],displayName:['','x'.repeat(201),'\udc00'],redirectUris:['','http://client.example/a','https://u:p@client.example/a','https://client.example/a#f',' https://client.example/a','https://CLIENT.example/a','https://client.example','https://client.example/%zz','https://client.example/a\nhttps://client.example/a',Array(11).fill(draft.redirectUris).join('\n')],credentialExpiresAt:['','2026-11-01T12:00:00Z','2030-02-30T00:00:00Z','2030-01-01T00:00:00+00:00','2030-01-01T00:00:00.000Z']}))for(const value of values)assert.ok(validate({...draft,[field]:value},now).errors[field]);
 assert.ok(validate({...draft,redirectUris:'x'.repeat(32769)},now).errors.redirectUris);
 for(const value of [null,3,'','\ud800x','x\udc00','\ud800'])assert.equal(text(value,400),false);
 assert.equal(text('😀',2),true);assert.equal(callback('not a URL'),false);assert.equal(callback('https://'),false);
 for(const value of [null,'0000-01-01T00:00:00Z','2030-13-01T00:00:00Z'])assert.equal(Number.isNaN(utc(value)),true);
 for(const id of ['.','..','ANTIFORGERY','antiforgery',''])assert.equal(addressable(id),false);
 assert.equal(addressable('opaque /?% identifier'),true);
});

test('Given_managed_and_readonly_details_When_observed_Then_only_exact_sanitized_projections_survive',async()=>{
 const value=await observe(read,details.id,now);assert.deepEqual(value,{details,expires:now+60000,etag:read.etag});assert.notEqual(value.details,details);
 const readonly={...details,editable:false,profile:null,credential:null,redirectUris:[],displayName:null};
 assert.deepEqual((await observe({status:200,value:readonly},details.id,now)).details,readonly);
 assert.ok(await observe({...read,etag:read.etag.replace('-1"','-2"')},details.id,now));
 assert.ok(await observe({...read,value:{...details,credential:{...details.credential,expiresAt:'2026-01-01T00:00:00Z',issuedAt:'2025-01-01T00:00:00Z'}}},details.id,now));
 for(const [status,expected] of [[400,'invalid'],[401,'expired'],[403,'forbidden'],[404,'missing'],[409,'conflict'],[503,'unavailable']])assert.equal(reason(status),expected);
});

test('Given_untrusted_details_When_shape_binding_or_deadline_is_invalid_Then_no_metadata_is_retained',async()=>{
 const variants=[null,[],{}, {...details,extra:secret}, {...details,id:''}, {...details,id:'other'}, {...details,clientId:''},{...details,displayName:3},{...details,displayName:'x'.repeat(201)},{...details,editable:'true'},{...details,redirectUris:null},{...details,profile:'other'},{...details,redirectUris:[]},{...details,redirectUris:Array(11).fill(draft.redirectUris)},{...details,redirectUris:[draft.redirectUris,draft.redirectUris]},{...details,redirectUris:['http://bad.example/']},{...details,credential:null},{...details,credential:{...details.credential,extra:true}},{...details,credential:{...details.credential,issuedAt:input.credentialExpiresAt}},{...details,expiresAt:'invalid'},{...details,expiresAt:'2026-11-01T12:00:00Z'},{...details,expiresAt:'2026-11-01T12:06:00Z'}];
 for(const value of variants)assert.equal(await observe({...read,value},details.id,now),null);
 for(const etag of [undefined,'','W/'+read.etag,'"'+'A'.repeat(64)+'-1"',tag('foreign'),'"'+'a'.repeat(64)+'-9223372036854775808"'])assert.equal(await observe({...read,etag},details.id,now),null);
 const readonly={...details,editable:false,profile:null,credential:null,redirectUris:[]};
 for(const value of [{...readonly,profile:'managed-web-v1'},{...readonly,credential:details.credential},{...readonly,redirectUris:details.redirectUris}])assert.equal(await observe({status:200,value},details.id,now),null);
 assert.equal(await observe({status:200,value:readonly,etag:read.etag},details.id,now),null);
});

test('Given_created_output_When_checked_Then_secret_and_requested_configuration_are_bound',async()=>{
 assert.equal((await created(success,input,now)).secret,secret);
 for(const value of [null,{}, {...success.value,extra:true},{...success.value,clientSecret:'short'},{...success.value,clientSecret:secret.slice(0,-2)+'B='}])assert.equal(await created({...success,value},input,now),null);
 assert.equal(await created({...success,status:200},input,now),null);
 for(const application of [{...details,clientId:'other'},{...details,displayName:'other'},{...details,redirectUris:['https://other.example/callback']},{...details,credential:{...details.credential,expiresAt:'2031-01-01T00:00:00Z'}},{...details,editable:false,profile:null,credential:null,redirectUris:[]}])assert.equal(await created({...success,value:{...success.value,application}},input,now),null);
 assert.equal(await created({...success,etag:read.etag.replace('-1"','-2"')},input,now),null);
 const expiredInput={...input,credentialExpiresAt:'2026-11-01T11:59:59Z'};
 assert.equal(await created({...success,value:{...success.value,application:{...details,credential:{issuedAt:'2026-11-01T11:00:00Z',expiresAt:expiredInput.credentialExpiresAt}}}},expiredInput,now),null);
});

test('Given_new_form_When_submitted_twice_Then_only_one_native_token_and_mutation_are_sent',async t=>{
 const pending=deferred();let posts=0;const {owner,states,calls}=setup(t,{token:()=>{calls.token++;return pending.promise},create:async(body,csrf)=>{posts++;assert.deepEqual(body,input);assert.equal(csrf,proof.value.requestToken);return success}});
 owner.newForm();const first=owner.create(draft);assert.equal(states.at(-1).kind,'working');await owner.create(draft);pending.resolve(proof);await first;
 assert.equal(calls.token,1);assert.equal(posts,1);assert.equal(states.at(-1).secret,secret);assert.equal(states.at(-1).expires,now+60000);
 owner.dismiss();assert.equal(states.at(-1).kind,'details');assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);owner.dismiss();owner.list();assert.deepEqual(states.at(-1),{kind:'list'});
});

test('Given_invalid_form_or_authority_When_submitted_Then_no_network_mutation_occurs',async t=>{
 const {owner,states,calls}=setup(t);await owner.create(draft);assert.equal(calls.token,0);
 owner.newForm();assert.ok((await owner.create({...draft,clientId:''})).clientId);assert.equal(calls.token,0);
 owner.authorize({kind:'anonymous'});owner.newForm();assert.deepEqual(states.at(-1),{kind:'problem',reason:'expired',outcome:'not-sent'});
 await owner.details(details.id);assert.equal(calls.details,0);owner.authorize(session);owner.newForm();owner.authorize({...session,subject:'different'});assert.equal(states.at(-1).reason,'expired');
 owner.dispose();const length=states.length;owner.authorize(session);owner.newForm();owner.list();await owner.details(details.id);await owner.create(draft);owner.suspend();owner.checkDeadline();assert.equal(states.length,length);
});

test('Given_preflight_failure_When_creation_is_prepared_Then_no_POST_is_sent',async t=>{
 for(const value of [{status:401},{status:403},{status:503},{status:200,value:null},{status:200,value:{requestToken:''}},{status:200,value:{requestToken:'x'.repeat(4097)}},{status:200,value:{requestToken:'bad\n'}},{status:200,value:{requestToken:3}}]){
  const {owner,states,calls}=setup(t,{token:async()=>value});owner.newForm();await owner.create(draft);assert.equal(calls.create,0);assert.equal(states.at(-1).outcome,'not-sent');owner.dispose();
 }
 const {owner,states}=setup(t,{token:async()=>{throw new Error(secret)}});owner.newForm();await owner.create(draft);assert.deepEqual(states.at(-1),{kind:'problem',reason:'unavailable',outcome:'not-sent'});
});

test('Given_dispatched_creation_When_any_response_is_unconfirmed_Then_outcome_is_ambiguous_without_retry',async t=>{
 for(const value of [{status:400},{status:401},{status:403},{status:409},{status:503},{...success,value:{}},{...success,etag:tag('foreign')}]){
  let posts=0;const {owner,states}=setup(t,{create:async()=>{posts++;return value}});owner.newForm();await owner.create(draft);assert.equal(states.at(-1).outcome,'unknown');await owner.create(draft);assert.equal(posts,1);assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);owner.dispose();
 }
 const {owner,states}=setup(t,{create:async()=>{throw new Error(secret)}});owner.newForm();await owner.create(draft);assert.deepEqual(states.at(-1),{kind:'problem',reason:'unavailable',outcome:'unknown'});
});

test('Given_original_deadline_When_token_or_response_arrives_late_Then_no_secret_or_renewal_is_published',async t=>{
 for(const phase of ['token','create']){
  let clock=now;const delayed=deferred();const overrides={[phase]:()=>delayed.promise};const {owner,states,calls}=setup(t,overrides,()=>clock);
  owner.newForm();const work=owner.create(draft);await Promise.resolve();clock=session.expires;delayed.resolve(phase==='token'?proof:success);await work;
  assert.equal(states.at(-1).reason,'expired');assert.equal(states.at(-1).outcome,phase==='token'?'not-sent':'unknown');assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);if(phase==='token')assert.equal(calls.create,0);owner.dispose();
 }
 let clock=now;const {owner,states}=setup(t,{},()=>clock);owner.newForm();clock=session.expires;await owner.create(draft);assert.equal(states.at(-1).reason,'expired');
});

test('Given_pending_mutation_When_view_or_authority_changes_Then_late_success_cannot_restore_secrets',async t=>{
 for(const action of ['suspend','dispose','list','newForm','details','authority'])for(const failure of [false,true]){
  const delayed=deferred();const {owner,states}=setup(t,{create:()=>delayed.promise});owner.newForm();const work=owner.create(draft);await Promise.resolve();
  if(action==='authority')owner.authorize({kind:'anonymous'});else if(action==='details')await owner.details(details.id);else owner[action]();
  const length=states.length;if(failure)delayed.reject(new Error(secret));else delayed.resolve(success);await work;assert.equal(states.length,length);assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);owner.dispose();
 }
});

test('Given_verified_details_When_read_or_superseded_Then_only_current_sanitized_data_is_visible',async t=>{
 const {owner,states}=setup(t);await owner.details(details.id);assert.equal(states.at(-1).kind,'details');assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);
 for(const value of [{status:401},{status:403},{status:404},{status:503},{status:200,value:{}}]){const item=setup(t,{details:async()=>value});await item.owner.details(details.id);assert.equal(item.states.at(-1).kind,'problem');item.owner.dispose();}
 for(const fail of [false,true]){const delayed=deferred();const item=setup(t,{details:()=>delayed.promise});const work=item.owner.details(details.id);item.owner.list();const length=item.states.length;if(fail)delayed.reject(new Error(secret));else delayed.resolve(read);await work;assert.equal(item.states.length,length);item.owner.dispose();}
 const failed=setup(t,{details:async()=>{throw new Error(secret)}});await failed.owner.details(details.id);assert.equal(failed.states.at(-1).reason,'unavailable');
});

test('Given_observation_crypto_wait_When_view_changes_Then_verified_but_stale_output_is_discarded',async t=>{
 const original=crypto.subtle.digest;let pending,entered=false;
 crypto.subtle.digest=function(...args){entered=true;return pending.promise.then(()=>original.apply(this,args))};
 try{for(const create of [false,true]){pending=deferred();entered=false;const {owner,states}=setup(t);if(create)owner.newForm();const work=create?owner.create(draft):owner.details(details.id);while(!entered)await Promise.resolve();owner.suspend();const length=states.length;pending.resolve();await work;assert.equal(states.length,length);owner.dispose();}}
 finally{crypto.subtle.digest=original;}
});

test('Given_secret_display_When_original_deadline_or_hiding_occurs_Then_current_state_drops_all_sensitive_values',async t=>{
 let clock=now;const {owner,states}=setup(t,{},()=>clock);owner.checkDeadline();owner.newForm();await owner.create(draft);owner.authorize(session);owner.checkDeadline();assert.equal(states.at(-1).kind,'created');clock=now+60000;owner.dismiss();assert.equal(states.at(-1).reason,'display-expired');assert.equal(states.at(-1).outcome,'created');assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);
 clock=now;owner.newForm();await owner.create(draft);owner.suspend();assert.deepEqual(states.at(-1),{kind:'problem',reason:'cleared',outcome:'created'});owner.suspend();assert.equal(states.at(-1).outcome,'created');
});

test('Given_display_timer_When_it_fires_Then_original_deadline_clears_without_polling',async t=>{
 t.mock.timers.enable({apis:['setTimeout','Date'],now});const {owner,states,calls}=setup(t,{},()=>Date.now());owner.newForm();await owner.create(draft);t.mock.timers.tick(60000);assert.equal(states.at(-1).reason,'display-expired');assert.equal(calls.create,1);assert.equal(calls.token,1);
});

test('Given_document_lifecycle_When_hidden_or_pagehide_fires_Then_synchronous_owner_erasure_precedes_return',async t=>{
 const page=new EventTarget(),document=new EventTarget();document.visibilityState='visible';const {owner,states}=setup(t);owner.newForm();await owner.create(draft);let synchronized=0;
 const remove=bindLifecycle(owner,page,document,action=>{synchronized++;action()});document.visibilityState='hidden';document.dispatchEvent(new Event('visibilitychange'));assert.equal(synchronized,1);assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);
 document.visibilityState='visible';document.dispatchEvent(new Event('visibilitychange'));page.dispatchEvent(new Event('pageshow'));page.dispatchEvent(new Event('pagehide'));assert.equal(synchronized,2);
 remove();page.dispatchEvent(new Event('pagehide'));document.visibilityState='hidden';document.dispatchEvent(new Event('visibilitychange'));assert.equal(synchronized,2);
});

test('Given_generated_transport_When_operations_run_Then_paths_proofs_and_credentials_are_fixed',async()=>{
 const original=globalThis.fetch;globalThis.window={location:{origin:'https://bff.example.test'}};const requests=[];
 globalThis.fetch=async request=>{requests.push(request);return new Response(JSON.stringify(request.method==='POST'?success.value:request.url.endsWith('antiforgery')?proof.value:details),{status:request.method==='POST'?201:200,headers})};
 try{const client=editorClient(),signal=new AbortController().signal;assert.deepEqual(await client.token(signal),proof);assert.equal((await client.details("opaque /!?'id",signal)).status,200);assert.deepEqual(await client.create(input,proof.value.requestToken,signal),success);
  assert.equal(requests[0].url,'https://bff.example.test/bff/api/v1/applications/antiforgery');assert.equal(new URL(requests[1].url).pathname,'/bff/api/v1/applications/opaque%20%2F%21%3F%27id');assert.equal(requests[2].url,'https://bff.example.test/bff/api/v1/applications');assert.equal(requests[2].method,'POST');assert.equal(requests[2].headers.get('content-type'),'application/json');assert.equal(requests[2].headers.get('X-CSRF-TOKEN'),proof.value.requestToken);assert.deepEqual(await requests[2].json(),input);
  for(const request of requests){assert.equal(request.credentials,'same-origin');assert.equal(request.cache,'no-store');assert.equal(request.redirect,'error');assert.equal(request.headers.has('authorization'),false)}
  await assert.rejects(()=>client.details('AnTiFoRgErY',signal));assert.equal(requests.length,3);
 }finally{globalThis.fetch=original;delete globalThis.window;}
});

test('Given_hostile_transport_When_media_headers_redirect_or_size_changes_Then_no_unbounded_or_private_body_survives',async()=>{
 const original=globalThis.fetch;globalThis.window={location:{origin:'https://bff.example.test'}};
 try{const client=editorClient(),signal=new AbortController().signal;
  for(const status of [400,401,403,409,503]){globalThis.fetch=async()=>new Response(secret,{status});assert.deepEqual(await client.token(signal),{status,value:undefined});}
  const invalid=[new Response('{}'),new Response('{}',{headers:{...headers,'cache-control':'public'}}),new Response('{}',{headers:{...headers,'x-content-type-options':'other'}}),new Response(null,{headers}),new Response('x'.repeat(524289),{headers}),new Response('{',{headers})];
  const redirected=new Response('{}',{headers});Object.defineProperty(redirected,'redirected',{value:true});invalid.push(redirected);
  for(const response of invalid){globalThis.fetch=async()=>response;await assert.rejects(()=>client.token(signal));}
  globalThis.fetch=async()=>new Response(new ReadableStream({start(controller){controller.error(new Error(secret))}}),{headers});await assert.rejects(()=>client.token(signal));
 }finally{globalThis.fetch=original;delete globalThis.window;}
});

test('Given_rendered_forms_and_details_When_reviewed_Then_labels_safe_text_and_one_time_output_are_explicit',()=>{
 const form=renderToStaticMarkup(createElement(CreateForm,{submit:async()=>({}),cancel:()=>{}}));
 for(const name of ['clientId','displayName','redirectUris','credentialExpiresAt']){assert.ok(form.includes(`for="editor-${name}"`));assert.ok(form.includes(`id="editor-${name}-help"`));}
 assert.ok(form.includes('YYYY-MM-DDTHH:mm:ssZ'));assert.ok(form.includes('replacement is not yet available'));assert.equal(form.includes('value="2030'),false);
 const visible=renderToStaticMarkup(createElement(DetailsView,{details:{...details,displayName:'<img src=x onerror=evil()>'},secret,dismiss:()=>{}}));assert.ok(visible.includes(secret));assert.ok(visible.includes('&lt;img'));assert.equal(visible.includes('<img'),false);assert.equal(visible.includes('aria-live'),false);assert.match(visible, /readonly=""/i);
 const hidden=renderToStaticMarkup(createElement(DetailsView,{details,dismiss:()=>{}}));assert.equal(hidden.includes(secret),false);assert.ok(hidden.includes('Details never return'));
 const readonly=renderToStaticMarkup(createElement(DetailsView,{details:{...details,editable:false,credential:null,redirectUris:[],displayName:null},dismiss:()=>{}}));assert.ok(readonly.includes('Read-only registration'));assert.ok(readonly.includes('Unnamed application'));
 for(const reason of ['expired','forbidden','missing','conflict','invalid','cleared','unavailable','display-expired'])for(const outcome of ['unknown','created','not-sent']){const html=renderToStaticMarkup(createElement(ProblemView,{state:{kind:'problem',reason,outcome}}));assert.equal(html.includes(secret),false);if(outcome==='unknown')assert.ok(html.includes('may have been created'));assert.equal(html.includes('Retry'),false);}
});


test('Given_ordered_server_callbacks_When_creation_used_reverse_order_Then_exact_set_is_accepted',async()=>{
 const callbacks=['https://z.example/callback','https://a.example/callback'];
 const requested={...input,redirectUris:callbacks};
 const result={...success,value:{...success.value,application:{...details,redirectUris:[...callbacks].sort()}}};
 assert.equal((await created(result,requested,now)).secret,secret);
 assert.deepEqual(requested.redirectUris,callbacks);
 for(const redirects of [[callbacks[0]],[...callbacks,'https://extra.example/callback'],[callbacks[0],'https://other.example/callback']])assert.equal(await created({...result,value:{...result.value,application:{...details,redirectUris:redirects}}},requested,now),null);
});

test('Given_independent_authority_and_credential_deadlines_When_secret_is_displayed_Then_earliest_original_deadline_wins',async t=>{
 for(const boundary of ['session','credential']){
  let clock=now;
  const expiry='2026-11-01T12:00:20Z';
  const result=boundary==='credential'?{...success,value:{...success.value,application:{...details,credential:{...details.credential,expiresAt:expiry}}}}:success;
  const {owner,states}=setup(t,{create:async()=>result},()=>clock);
  if(boundary==='session')owner.authorize({...session,expires:now+20000});
  owner.newForm();await owner.create(boundary==='credential'?{...draft,credentialExpiresAt:expiry}:draft);
  assert.equal(states.at(-1).kind,'created');assert.equal(states.at(-1).expires,now+20000);
  clock=now+20000;owner.checkDeadline();assert.equal(states.at(-1).reason,boundary==='session'?'expired':'display-expired');assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);owner.dispose();
 }
});

test('Given_pending_native_proof_When_owner_changes_Then_late_proof_cannot_dispatch_a_mutation',async t=>{
 for(const action of ['suspend','newForm','authority','dispose']){
  const pending=deferred();const {owner,states,calls}=setup(t,{token:()=>pending.promise});owner.newForm();const work=owner.create(draft);
  if(action==='authority')owner.authorize({...session,subject:'other'});else owner[action]();
  const length=states.length;pending.resolve(proof);await work;assert.equal(calls.create,0);assert.equal(states.length,length);owner.dispose();
 }
});

test('Given_crypto_verification_in_flight_When_original_deadline_passes_Then_no_response_is_published',async t=>{
 const original=crypto.subtle.digest;
 try{for(const create of [false,true]){
  const pending=deferred();let entered=false,clock=now;
  crypto.subtle.digest=function(...args){entered=true;return pending.promise.then(()=>original.apply(this,args))};
  const {owner,states}=setup(t,{},()=>clock);if(create)owner.newForm();
  const work=create?owner.create(draft):owner.details(details.id);while(!entered)await Promise.resolve();
  clock=session.expires;pending.resolve();await work;assert.equal(states.at(-1).reason,'expired');assert.equal(states.at(-1).outcome,create?'unknown':'not-sent');assert.equal(JSON.stringify(states.at(-1)).includes(secret),false);owner.dispose();
 }}finally{crypto.subtle.digest=original;}
});


test('Given_failed_local_validation_When_fields_render_Then_each_error_has_label_description_and_summary_link',()=>{
 const invalid={...draft,clientId:'',displayName:'',redirectUris:'http://bad.example/',credentialExpiresAt:''};
 const errors=validate(invalid,now).errors;
 const html=renderToStaticMarkup(createElement(CreateFields,{draft:invalid,errors,setDraft:()=>{},summary:{current:null}}));
 assert.equal(Object.keys(errors).length,4);assert.ok(html.includes('role="alert"'));assert.ok(html.includes('tabindex="-1"'));
 for(const name of Object.keys(errors)){
  assert.ok(html.includes(`href="#editor-${name}"`));assert.ok(html.includes(`for="editor-${name}"`));assert.ok(html.includes(`id="editor-${name}-error"`));
  assert.ok(html.includes(`aria-describedby="editor-${name}-help editor-${name}-error"`));
 }
 assert.equal((html.match(/aria-invalid="true"/g)||[]).length,4);
});

test('Given_expired_session_When_returning_to_list_Then_focus_uses_enabled_button_or_stable_heading',()=>{
 const calls=[],heading={focus:()=>calls.push('heading')};
 focusList({disabled:false,focus:()=>calls.push('button')},heading);
 focusList({disabled:true,focus:()=>assert.fail('disabled target')},heading);
 focusList(null,heading);focusList(null,null);assert.deepEqual(calls,['button','heading','heading']);
 const html=renderToStaticMarkup(createElement(ProblemView,{state:{kind:'problem',reason:'display-expired',outcome:'created'}}));
 assert.ok(html.includes('Return to applications'));assert.equal(html.includes('href="/bff/login"'),false);assert.equal(html.includes('Your session observation has ended'),false);
});

test('Given_credential_expiring_during_preflight_When_native_proof_arrives_Then_no_POST_is_sent',async t=>{
 let clock=now;const pending=deferred();const {owner,states,calls}=setup(t,{token:()=>pending.promise},()=>clock);
 owner.newForm();const work=owner.create({...draft,credentialExpiresAt:'2026-11-01T12:00:20Z'});clock=now+20000;pending.resolve(proof);await work;
 assert.equal(calls.create,0);assert.deepEqual(states.at(-1),{kind:'problem',reason:'invalid',outcome:'not-sent'});
});

test('Given_managed_metadata_When_names_or_whole_second_instants_are_invalid_Then_response_is_rejected',async()=>{
 for(const value of [{...details,clientId:'with space'},{...details,displayName:null},{...details,displayName:''},{...details,displayName:'control\u0000'},{...details,credential:{...details.credential,issuedAt:'2026-11-01T12:00:00.1Z'}},{...details,credential:{...details.credential,expiresAt:'2030-01-01T00:00:00.1Z'}}])assert.equal(await observe({...read,value},details.id,now),null);
 assert.ok(await observe({...read,value:{...details,credential:{issuedAt:'2026-11-01T12:00:00+00:00',expiresAt:'2030-01-01T00:00:00+00:00'}}},details.id,now));
});
