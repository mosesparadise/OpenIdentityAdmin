import { test } from 'node:test';
import assert from 'node:assert/strict';
import { observe } from './observation.mjs';
import { ApplicationController } from './controller.mjs';
import { readApplications } from './client.mjs';
const now = Date.parse('2026-10-29T12:00:00Z');
const query = { search: '', page: 1, pageSize: 25 };
const item = { id: 'opaque', clientId: 'admin', displayName: '<script>literal</script>', clientType: 'confidential' };
const page = { items: [item], page: 1, pageSize: 25, hasNextPage: false, expiresAt: '2026-10-29T12:01:00Z' };
const live = { status: 200, value: page };
const deferred = () => { let resolve, reject; const promise = new Promise((a,b) => { resolve=a; reject=b; }); return { promise,resolve,reject }; };

test('Given_collection_When_observed_Then_only_current_exact_summary_is_retained', () => {
  assert.deepEqual(observe(live, query, now), { kind: 'ready', items: [item], page: 1, pageSize: 25, hasNextPage: false, expires: now + 60_000 });
  assert.equal(observe({ status:200, value:{...page,items:[]} },query,now).kind,'ready');
  assert.equal(observe({ status:200, value:{...page,items:[{...item,displayName:null,clientType:null}]} },query,now).kind,'ready');
  assert.equal(observe(live,query,now+60_000).kind,'expired');
  assert.equal(observe({status:401,value:page},query,now).kind,'expired');
  assert.equal(observe({status:403,value:page},query,now).kind,'forbidden');
  assert.equal(observe({status:503,value:page},query,now).kind,'unavailable');
});

test('Given_malformed_or_oversized_collection_When_observed_Then_all_data_is_rejected', () => {
  for(const value of [null,[],true,{}, {...page,extra:'credential'}, {...page,page:2}, {...page,pageSize:100},
    {...page,hasNextPage:'true'}, {...page,hasNextPage:true}, {...page,items:null}, {...page,items:Array(26).fill(item)},
    {...page,expiresAt:null}, {...page,expiresAt:'2026-02-30T12:00:00Z'}, {...page,expiresAt:'2026-13-01T00:00:00Z'},
    {...page,expiresAt:'2026-10-29T12:06:00Z'}, {...page,expiresAt:'2026-10-29T12:01:00+01:00'},
    {...page,items:[item,item]}, ...[null,[],{}, {...item,extra:'secret'}, {...item,id:''}, {...item,id:'x'.repeat(401)},
      {...item,clientId:''}, {...item,clientId:'x'.repeat(101)}, {...item,displayName:'x'.repeat(201)},
      {...item,clientType:'x'.repeat(51)}, {...item,displayName:undefined}, {...item,clientType:3}].map(value=>({...page,items:[value]}))])
    assert.deepEqual(observe({status:200,value},query,now),{kind:'unavailable'});
  assert.equal(observe(live,{...query,search:'Admin'},now).kind,'unavailable');
  assert.equal(observe(live,{...query,search:'adm'},now).kind,'ready');
  assert.equal(observe({status:200,value:{...page,pageSize:1,hasNextPage:true}},{...query,pageSize:1},now).kind,'ready');
});

test('Given_overlapping_searches_When_old_response_finishes_Then_only_latest_collection_survives', async () => {
  for(const failure of [false,true]) {
    const old=deferred(); let count=0; const states=[]; const signals=[];
    const owner=new ApplicationController((q,s)=>{signals.push(s);return count++===0?old.promise:Promise.resolve(live)},s=>states.push(s),()=>now);
    const pending=owner.check(query); await owner.check(query); assert.equal(signals[0].aborted,true);
    if(failure) old.reject(new Error('private')); else old.resolve({status:401}); await pending;
    assert.equal(states.at(-1).kind,'ready'); owner.dispose();
  }
});

test('Given_failed_read_When_reloaded_Then_previous_collection_is_cleared',async()=>{
  let fail=false;const states=[];const owner=new ApplicationController(async()=>{if(fail)throw new Error('private');return live},s=>states.push(s),()=>now);
  await owner.check(query);fail=true;await owner.check(query);assert.deepEqual(states.slice(-2),[{kind:'loading'},{kind:'unavailable'}]);owner.dispose();
});

test('Given_lifecycle_boundary_When_late_result_arrives_Then_suspended_or_disposed_data_cannot_return',async()=>{
  for(const stop of ['suspend','dispose'])for(const fail of [true,false]){
    const pending=deferred();const states=[];let loads=0;const owner=new ApplicationController(()=>{loads++;return pending.promise},s=>states.push(s),()=>now);
    const read=owner.check(query);owner[stop]();const count=states.length;
    if(fail)pending.reject(new Error('private'));else pending.resolve(live);await read;assert.equal(states.length,count);
    if(stop==='dispose'){await owner.check(query);assert.equal(loads,1);}else{owner.dispose();}
  }
});

test('Given_throttled_timer_When_original_expiry_is_reached_Then_collection_is_erased',async()=>{
  let clock=now;const states=[];const owner=new ApplicationController(async()=>live,s=>states.push(s),()=>clock);
  owner.checkDeadline();await owner.check(query);owner.checkDeadline();assert.equal(states.at(-1).kind,'ready');
  clock+=60_000;owner.checkDeadline();assert.deepEqual(states.at(-1),{kind:'expired'});owner.dispose();
});

test('Given_original_deadline_When_timer_runs_Then_no_refresh_or_poll_occurs',async()=>{
  const original=globalThis.setTimeout;let callback;let clock=now;let reads=0;const states=[];
  globalThis.setTimeout=(fn,delay)=>{callback=fn;assert.equal(delay,60_000);return 1;};
  try{const owner=new ApplicationController(async()=>{reads++;return live},s=>states.push(s),()=>clock);
    await owner.check(query);clock+=60_000;callback();assert.equal(states.at(-1).kind,'expired');assert.equal(reads,1);owner.dispose();
  }finally{globalThis.setTimeout=original;}
});

test('Given_generated_client_When_collection_requested_Then_only_fixed_same_origin_cookie_read_is_sent',async()=>{
  const original=globalThis.fetch;globalThis.window={location:{origin:'https://bff.example.test'}};let request;
  globalThis.fetch=async value=>{request=value;return new Response(JSON.stringify(page),{headers:{'content-type':'application/json'}})};
  try{assert.deepEqual(await readApplications({...query,search:'%_\\'},new AbortController().signal),live);
    const uri=new URL(request.url);assert.equal(uri.origin,window.location.origin);assert.equal(uri.pathname,'/bff/api/v1/applications');assert.equal(uri.searchParams.get('search'),'%_\\');
    assert.equal(request.credentials,'same-origin');assert.equal(request.cache,'no-store');assert.equal(request.redirect,'error');assert.equal(request.headers.has('authorization'),false);
  }finally{globalThis.fetch=original;delete globalThis.window;}
});

test('Given_untrusted_transport_When_error_media_or_size_is_invalid_Then_no_raw_error_body_is_retained',async()=>{
  const original=globalThis.fetch;globalThis.window={location:{origin:'https://bff.example.test'}};
  try{
    for(const status of [401,403,503]){globalThis.fetch=async()=>new Response('private-error',{status});assert.deepEqual(await readApplications(query,new AbortController().signal),{status,value:undefined});}
    for(const response of [new Response('<html>'),new Response('x'.repeat(524289),{headers:{'content-type':'application/json'}}),new Response(null,{headers:{'content-type':'application/json'}})]){
      globalThis.fetch=async()=>response;await assert.rejects(()=>readApplications(query,new AbortController().signal));
    }
    const redirected=new Response('{}');Object.defineProperty(redirected,'redirected',{value:true});globalThis.fetch=async()=>redirected;
    await assert.rejects(()=>readApplications(query,new AbortController().signal));
  }finally{globalThis.fetch=original;delete globalThis.window;}
});
