import fs from 'node:fs/promises'
import crypto from 'node:crypto'
const read = async p => JSON.parse((await fs.readFile(p,'utf8')).replace(/^\uFEFF/,''))
const input = await read('.tools/items567-workbook.json')
const [contracts,rates,issues,refs,stageCounts] = await read('.tools/items567-review.json')
const [liveRates,liveContracts] = await read('.tools/items567-legacy.json')
const norm = v => String(v??'').trim()
const day = v => norm(v).slice(0,10)
const key = (kind,id) => `${kind}:${norm(id)}`
const stageByKey = new Map([...contracts.map(r=>[key('Contract',r.SourceId),r]),...rates.map(r=>[key('ContractHistory',r.SourceId),r])])
const liveByKey = new Map([...liveContracts.map(r=>[key('Contract',r.ContractID),r]),...liveRates.map(r=>[key('ContractHistory',r.ContractHistoryID),r])])
const byContract = new Map(contracts.map(r=>[norm(r.SourceId),r]))
const related = id => rates.filter(r=>norm(r.SourceContractId)===id)
for(const r of [...contracts,...rates]) if(crypto.createHash('sha256').update(r.Payload).digest('hex').toUpperCase()!==r.PayloadHash)throw Error('Kaynak hash hatası')
const rows = input.sheets.flatMap(sheet=>sheet.rows.map(r=>{
    const historyId=norm(r['Legacy Tarihçe No']),contractId=norm(r['Legacy Sözleşme No'])
    const entity=historyId?'ContractHistory':'Contract',id=historyId||contractId,entityKey=key(entity,id)
    const stage=stageByKey.get(entityKey),raw=stage?JSON.parse(stage.Payload):null,live=liveByKey.get(entityKey)
    const parent=byContract.get(contractId),parentRaw=parent?JSON.parse(parent.Payload):null
    const note=norm(r[sheet.noteKey]),upper=note.toLocaleUpperCase('tr-TR')
    const action=!upper?'UNANSWERED':upper.includes('KALSIN')?'KEEP':upper.includes('SİL')?'EXCLUDE':'REVIEW_NOTE'
    const fields=[['Legacy Sözleşme No','ContractID'],['Legacy Müşteri No','CustomerID'],['Başlangıç Ay','StartingDateMonth'],['Başlangıç Yıl','StartingDateYear'],['Bitiş Tarihi','EndDate'],['Ödeme Dönemi ID','PaymentTypeID']]
    if(historyId)fields.push(['Legacy Tarihçe No','ContractHistoryID'])
    fields.push(['Tutar' in r?'Tutar':'Legacy Tutar','Amount'],['Para Birimi ID' in r?'Para Birimi ID':'Legacy Para Birimi ID','CurrencyID'])
    if(historyId)fields.push(['Legacy İşlem Türü' in r?'Legacy İşlem Türü':'İşlem Türü','ProcessType'],['Açıklama' in r?'Açıklama':'Legacy Açıklama','Description'])
    const same=(a,b,k)=>k==='EndDate'?day(a)===day(b):k==='Amount'&&norm(a)&&norm(b)?Number(a)===Number(b):norm(a)===norm(b)
    const workbookMismatch=raw?fields.filter(([h,k])=>!same(r[h],raw[k],k)).map(([h])=>h):['Kaynak yok']
    const liveMismatch=live&&raw?fields.filter(([,k])=>!same(raw[k],live[k],k)).map(([,k])=>k):['Canlı kaynak yok']
    const ownIssues=issues.filter(i=>i.EntityCode===entity&&norm(i.SourceId)===id)
    const parentIssues=issues.filter(i=>i.EntityCode==='Contract'&&norm(i.SourceId)===contractId)
    const itemFour=parentIssues.some(i=>i.Status===1&&['CUSTOMER_DUPLICATE_SUPERSEDED','CUSTOMER_NAME_SUPERSEDED','CUSTOMER_IMPORTED_SUPERSEDED'].includes(i.IssueCode))
    return {sheet:sheet.name,excelRow:r.excelRow,entity,id,entityKey,contractId,historyId,note,action,
        status:stage?.Status??null,parentStatus:parent?.Status??null,itemFour,parentExists:!!parent,
        sourceSubscription:parentRaw?.SubscriptionStatusID??null,liveSubscription:liveContracts.find(c=>String(c.ContractID)===contractId)?.SubscriptionStatusID??null,
        workbookMismatch,liveMismatch,sourceAmount:raw?.Amount,sourceCurrency:raw?.CurrencyID,
        openIssues:ownIssues.filter(i=>i.Status===0).map(i=>i.IssueCode),parentIssues:parentIssues.filter(i=>i.Status===0).map(i=>i.IssueCode)}
}))
const groups=new Map()
for(const r of rows){const group=groups.get(r.entityKey)||[];group.push(r);groups.set(r.entityKey,group)}
const directConflicts=[...groups].filter(([,g])=>g.some(r=>r.action==='KEEP')&&g.some(r=>r.action==='EXCLUDE')).map(([entityKey,g])=>({entityKey,contractId:g[0].contractId,rows:g.map(r=>({sheet:r.sheet,row:r.excelRow,action:r.action}))}))
const contractConflicts=[...new Set(rows.filter(r=>r.entity==='Contract'&&r.action==='EXCLUDE').map(r=>r.contractId))]
    .filter(id=>rows.some(r=>r.contractId===id&&r.entity==='ContractHistory'&&r.action==='KEEP'))
const decisions=[...groups].map(([entityKey,g])=>{
    const r=g[0],hasKeep=g.some(r=>r.action==='KEEP'),hasDrop=g.some(r=>r.action==='EXCLUDE')
    const conflicts=directConflicts.some(c=>c.entityKey===entityKey)||contractConflicts.includes(r.contractId)
    let decision=r.itemFour?'PRESERVE_ITEM_FOUR_EXCLUSION':conflicts?'HOLD_CONFLICT':r.status===5||r.parentStatus===5?'PRESERVE_PREVIOUS_EXCLUSION':hasDrop?'EXCLUDE_CANDIDATE':hasKeep?'KEEP_WITH_VALIDATION':'HOLD_UNANSWERED'
    const blockers=[]
    if(g.some(r=>r.workbookMismatch.length))blockers.push('WORKBOOK_SOURCE_MISMATCH')
    if(g.some(r=>r.liveMismatch.length))blockers.push('LIVE_SOURCE_DRIFT')
    if(!r.parentExists)blockers.push('MISSING_PARENT')
    if(r.status===3||r.status===4)blockers.push('ALREADY_APPLIED')
    return {entityKey,entity:r.entity,id:r.id,contractId:r.contractId,decision,blockers,notes:g.map(r=>({sheet:r.sheet,row:r.excelRow,note:r.note})),openIssues:[...new Set(g.flatMap(r=>r.openIssues))]}
})
const countBy=(list,selector)=>list.reduce((o,r)=>{const k=selector(r);o[k]=(o[k]||0)+1;return o},{})
const summary={hash:input.hash,totalRows:rows.length,uniqueEntities:groups.size,uniqueContracts:new Set(rows.map(r=>r.contractId)).size,
    perSheet:input.sheets.map(s=>({sheet:s.name,rows:s.rows.length,actions:countBy(rows.filter(r=>r.sheet===s.name),r=>r.action)})),
    entityTypes:countBy(decisions,r=>r.entity),decisions:countBy(decisions,r=>r.decision),
    uniqueDirectConflicts:directConflicts.length,contractConflicts,
    workbookMismatches:rows.filter(r=>r.workbookMismatch.length).map(r=>({key:r.entityKey,sheet:r.sheet,row:r.excelRow,fields:r.workbookMismatch})),
    liveChanges:[...new Set(rows.filter(r=>r.liveMismatch.length).map(r=>r.entityKey))],
    keepPreviouslyExcluded:rows.filter(r=>r.action==='KEEP'&&(r.status===5||r.parentStatus===5)).length,
    appliedReviewedEntities:decisions.filter(d=>d.blockers.includes('ALREADY_APPLIED')).length,
    donukNotesVsLive:countBy(rows.filter(r=>r.note.toLocaleUpperCase('tr-TR').includes('DONUK')),r=>String(r.liveSubscription)),
    priorItemFive:rows.filter(r=>r.sheet.startsWith('5')).map(r=>({id:r.id,contract:r.contractId,decision:decisions.find(d=>d.entityKey===r.entityKey)?.decision,allNotes:decisions.find(d=>d.entityKey===r.entityKey)?.notes})),
    keepIssues:countBy(decisions.filter(d=>d.decision==='KEEP_WITH_VALIDATION').flatMap(d=>d.openIssues),r=>r),
    stageCounts}
await fs.writeFile('.tools/items567-decisions.json',JSON.stringify({summary,directConflicts,contractConflicts,decisions,rows},null,2))
console.log(JSON.stringify(summary,null,2))
console.log('DIRECT CONFLICTS',JSON.stringify(directConflicts))
console.log('EXCLUDE TARGET COUNTS',JSON.stringify(countBy(decisions.filter(d=>d.decision==='EXCLUDE_CANDIDATE'),d=>`${d.entity}/${stageByKey.get(d.entityKey)?.Status}`)))
const detail={
    exclusionsBySubscription:countBy(decisions.filter(d=>d.decision==='EXCLUDE_CANDIDATE'),d=>String(rows.find(r=>r.entityKey===d.entityKey)?.liveSubscription)),
    exclusionsWithDonukNote:countBy(decisions.filter(d=>d.decision==='EXCLUDE_CANDIDATE'&&d.notes.some(n=>n.note.toLocaleUpperCase('tr-TR').includes('DONUK'))),d=>String(rows.find(r=>r.entityKey===d.entityKey)?.liveSubscription)),
    keep7:countBy(rows.filter(r=>r.sheet.startsWith('7')&&r.action==='KEEP'),r=>`${r.sourceCurrency??'NULL'}/${decisions.find(d=>d.entityKey===r.entityKey)?.decision}`),
    changes:summary.liveChanges.map(k=>{const s=stageByKey.get(k),raw=JSON.parse(s.Payload),live=liveByKey.get(k),r=rows.find(r=>r.entityKey===k);return {key:k,decision:decisions.find(d=>d.entityKey===k)?.decision,fields:r.liveMismatch.map(f=>({field:f,snapshot:raw[f],live:live?.[f]}))}}),
    conflicts:contractConflicts.map(contract=>({contract,rows:rows.filter(r=>r.contractId===contract).map(r=>({entity:r.entity,id:r.id,sheet:r.sheet,row:r.excelRow,note:r.note}))})),
    exclusionsSourceDrift:decisions.filter(d=>d.decision==='EXCLUDE_CANDIDATE'&&d.blockers.includes('LIVE_SOURCE_DRIFT')).map(d=>d.entityKey),
    itemFourKeep:rows.filter(r=>r.itemFour&&r.action==='KEEP').map(r=>({entityKey:r.entityKey,contractId:r.contractId,sheet:r.sheet,row:r.excelRow})),
    candidate5:rows.filter(r=>r.sheet.startsWith('5')&&['34133','82291','84457'].includes(r.id)).map(r=>({contract:r.contractId,history:r.id,allTariffs:related(r.contractId).length,otherNotes:rows.filter(x=>x.contractId===r.contractId&&!x.sheet.startsWith('5')).map(x=>({sheet:x.sheet,row:x.excelRow,note:x.note}))}))
}
await fs.writeFile('.tools/items567-decision-details.json',JSON.stringify(detail,null,2))
console.log('DETAIL',JSON.stringify(detail,null,2))
