import fs from 'node:fs/promises'
import path from 'node:path'
import { FileBlob, SpreadsheetFile, Workbook } from '@oai/artifact-tool'

const sourcePath='C:/Users/Mehmet Zeki KARA/Downloads/Tahsilat_Musteri_Karar_Listeleri.xlsx'
const outputDir='D:/MGS/Repos/AssistFlow/AssistFlow-BE/outputs/tahsilat-guncel-karar-listeleri-20260923'
const outputPath=path.join(outputDir,'Tahsilat_Guncel_Karar_Listeleri_1_4_5_6_7.xlsx')
const normalize=value=>String(value??'').trim()
const normalizeSubscriber=value=>normalize(value).toLocaleUpperCase('tr-TR')
const colName=n=>{let s='';while(n){n--;s=String.fromCharCode(65+n%26)+s;n=Math.floor(n/26)}return s}

const contracts=new Map()
for(const line of (await fs.readFile('.artifact-work/contract-subscription.csv','utf8')).replace(/^\uFEFF/,'').split(/\r?\n/)){
    if(!/^\s*\d+\s*;/.test(line))continue
    const [id,status,stage]=line.split(';')
    contracts.set(normalize(id),{subscription:normalize(status),stage:normalize(stage)})
}
const customers=new Map(), groups=new Map()
for(const line of (await fs.readFile('.artifact-work/customer-created.csv','latin1')).split(/\r?\n/)){
    if(!/^\s*\d+\s*\|/.test(line))continue
    const [idRaw,subscriberRaw,,createdRaw]=line.split('|')
    const id=normalize(idRaw),subscriber=normalizeSubscriber(subscriberRaw),createdText=normalize(createdRaw),createdTime=Date.parse(createdText)
    const customer={id,subscriber,createdTime:Number.isFinite(createdTime)?createdTime:null}
    customers.set(id,customer)
    if(subscriber){if(!groups.has(subscriber))groups.set(subscriber,[]);groups.get(subscriber).push(customer)}
}

const source=await SpreadsheetFile.importXlsx(await FileBlob.load(sourcePath))
function readSheet(name){
    const values=source.worksheets.getItem(name).getUsedRange(true).values
    const headerIndex=values.findIndex(r=>r.some(c=>normalize(c)==='Legacy Sözleşme No'))
    const headers=values[headerIndex].map(normalize)
    const index=Object.fromEntries(headers.map((h,i)=>[h,i]))
    const rows=values.slice(headerIndex+1).filter(r=>r.some(c=>normalize(c)))
    return {headers,index,rows}
}
function membership(contractId){
    const value=contracts.get(normalize(contractId))?.subscription
    if(value==='12')return 'Aktif'
    if(value==='13')return 'Donuk'
    if(!contracts.has(normalize(contractId)))return 'Kaynak sözleşme bulunamadı'
    return 'Üyelik durumu boş/belirsiz'
}
const membershipSpecs=[
    {name:'1 - Sözleşme Durumu',title:'Madde 1 - Donuk kayıtlar çıkarılmış sözleşme listesi',decisions:['Tahsilata dahil et','Aktarım dışında bırak','İncelenecek']},
    {name:'5 - Null İşlem Türü',title:'Madde 5 - Donuk kayıtlar çıkarılmış null işlem türleri',decisions:['Normal ücretli dönem','Ücretsiz dönem','Hizmet dondurma','Aktarım dışında bırak','İncelenecek']},
    {name:'6 - Tarife Çakışmaları',title:'Madde 6 - Donuk kayıtlar çıkarılmış tarife istisnaları',decisions:['Güncel sözleşmeyi esas al','Son tarihçeyi esas al','Kayıtları manuel düzelt','Aktarım dışında bırak','İncelenecek']},
    {name:'7 - Para Birimi Tutar',title:'Madde 7 - Donuk kayıtlar çıkarılmış para birimi ve tutar listesi',decisions:['Bilgiyi tamamla','Aktarım dışında bırak','İncelenecek']},
]
const prepared=[]
for(const spec of membershipSpecs){
    const data=readSheet(spec.name)
    const contractCol=data.index['Legacy Sözleşme No']
    const kept=data.rows.filter(r=>membership(r[contractCol])!=='Donuk')
    const removed=data.rows.length-kept.length
    const techIndex=data.index['Teknik Referans']
    const headers=[...data.headers.slice(0,techIndex),'Üyelik Durumu',...data.headers.slice(techIndex)]
    const rows=kept.map(r=>[...r.slice(0,techIndex),membership(r[contractCol]),...r.slice(techIndex)])
    const uncertain=rows.filter(r=>r[techIndex]==='Kaynak sözleşme bulunamadı'||r[techIndex]==='Üyelik durumu boş/belirsiz').length
    prepared.push({...spec,headers,rows,original:data.rows.length,removed,uncertain})
}

const item4=readSheet('4 - Müşteri Eşleştirme')
const h=item4.index, item4Rows=[]
let removedOld=0,retainedNewest=0,unresolvedDuplicate=0
for(const row of item4.rows){
    const issueCodes=normalize(row[h['Teknik Referans']]).split(',').filter(Boolean)
    const isDuplicate=issueCodes.includes('SUBSCRIBER_SOURCE_DUPLICATE')
    const customerId=normalize(row[h['Legacy Müşteri No']]),customer=customers.get(customerId)
    let latestDate=null,status='Duplicate değil',decision=normalize(row[h['Müşteri Kararı']]),technical=normalize(row[h['Teknik Referans']])
    if(isDuplicate){
        const subscriber=customer?.subscriber||normalizeSubscriber(row[h['Legacy Abone No']])
        const group=groups.get(subscriber)??[],dated=group.filter(c=>c.createdTime!==null)
        const maxDate=dated.length?Math.max(...dated.map(c=>c.createdTime)):null
        const newest=maxDate===null?[]:dated.filter(c=>c.createdTime===maxDate)
        latestDate=maxDate===null?null:new Date(maxDate)
        if(!customer||customer.createdTime===null||newest.length!==1){unresolvedDuplicate++;status='Tarih eşit veya eksik - incelenecek'}
        else if(customer.id===newest[0].id){retainedNewest++;status='En yeni kayıt - kullanılacak';decision='En yeni kayıt kullanılacak';technical='SUBSCRIBER_SOURCE_DUPLICATE_RESOLVED_NEWEST'}
        else{removedOld++;continue}
    }
    item4Rows.push([
        normalize(row[h['Legacy Sözleşme No']]),customerId,normalize(row[h['Legacy Müşteri Adı']]),normalize(row[h['Legacy Abone No']]),
        customer?.createdTime===null||customer?.createdTime===undefined?null:new Date(customer.createdTime),latestDate,
        normalize(row[h['Bulunan AssistFlow Müşteri ID']]),normalize(row[h['Sorun Açıklaması']]),status,decision,
        normalize(row[h['Doğru SubscriberCode']]),normalize(row[h['Doğru AssistFlow Müşteri ID']]),normalize(row[h['Doğru Müşteri Adı']]),normalize(row[h['Müşteri Açıklaması']]),technical,
    ])
}
prepared.splice(1,0,{
    name:'4 - Müşteri Eşleştirme',title:'Madde 4 - En yeni duplicate kayıtlar korunmuş müşteri listesi',
    headers:['Legacy Sözleşme No','Legacy Müşteri No','Legacy Müşteri Adı','Legacy Abone No','Müşteri Kayıt Tarihi','Aynı Abone No En Yeni Tarih','Bulunan AssistFlow Müşteri ID','Sorun Açıklaması','Duplicate Değerlendirmesi','Müşteri Kararı','Doğru SubscriberCode','Doğru AssistFlow Müşteri ID','Doğru Müşteri Adı','Müşteri Açıklaması','Teknik Referans'],
    rows:item4Rows,original:item4.rows.length,removed:removedOld,uncertain:unresolvedDuplicate,retainedNewest,
    decisions:['Mevcut müşteriyle eşleştir','Yeni müşteri oluştur','Aktarım dışında bırak','İncelenecek','En yeni kayıt kullanılacak'],
})

const workbook=Workbook.create()
const summaries={}
for(let i=0;i<prepared.length;i++){
    const spec=prepared[i],sheet=workbook.worksheets.add(spec.name),lastCol=colName(spec.headers.length),lastRow=spec.rows.length+5
    sheet.showGridLines=false;sheet.tabColor=i%2?'#5B9BD5':'#1F4E78'
    sheet.getRange(`A1:${lastCol}1`).merge();sheet.getRange('A1').values=[[spec.title]]
    sheet.getRange(`A1:${lastCol}1`).format={fill:'#1F4E78',font:{name:'Arial',size:15,bold:true,color:'#FFFFFF'},rowHeight:28,verticalAlignment:'center'}
    const detail=spec.name.startsWith('4 -')
        ? `${spec.removed.toLocaleString('tr-TR')} eski duplicate satır çıkarıldı. ${spec.retainedNewest.toLocaleString('tr-TR')} en yeni satır kullanılacak. ${spec.uncertain.toLocaleString('tr-TR')} duplicate satır incelenecek.`
        : `${spec.removed.toLocaleString('tr-TR')} Donuk satır çıkarıldı. ${spec.rows.length.toLocaleString('tr-TR')} satır kaldı. ${spec.uncertain.toLocaleString('tr-TR')} satırın kaynak sözleşmesi veya üyelik durumu doğrulanamadığı için incelemede tutuldu.`
    sheet.getRange(`A2:${lastCol}2`).merge();sheet.getRange('A2').values=[[detail]]
    sheet.getRange(`A2:${lastCol}2`).format={fill:'#D9EAF7',font:{name:'Arial',size:10,italic:true,color:'#1F1F1F'},wrapText:true,rowHeight:34,verticalAlignment:'center'}
    sheet.getRange(`A3:${lastCol}3`).merge();sheet.getRange('A3').values=[[`Önceki liste: ${spec.original.toLocaleString('tr-TR')} satır. Güncel liste: ${spec.rows.length.toLocaleString('tr-TR')} satır.`]]
    sheet.getRange(`A3:${lastCol}3`).format={font:{name:'Arial',size:10,color:'#595959'},rowHeight:24,verticalAlignment:'center'}
    sheet.getRange(`A5:${lastCol}5`).values=[spec.headers];sheet.getRange(`A6:${lastCol}${lastRow}`).values=spec.rows
    const table=sheet.tables.add(`A5:${lastCol}${lastRow}`,true,`Madde${spec.name.slice(0,1)}GuncelTablosu`);table.style='TableStyleMedium2';table.showFilterButton=true
    sheet.freezePanes.freezeRows(5);sheet.freezePanes.freezeColumns(2)
    sheet.getRange(`A5:${lastCol}${lastRow}`).format.font={name:'Arial',size:9}
    sheet.getRange(`A5:${lastCol}5`).format={fill:'#1F4E78',font:{name:'Arial',size:9,bold:true,color:'#FFFFFF'},wrapText:true,horizontalAlignment:'center',verticalAlignment:'center',rowHeight:44}
    sheet.getRange(`A6:${lastCol}${lastRow}`).format.verticalAlignment='top';sheet.getRange(`A6:${lastCol}${lastRow}`).format.wrapText=true
    spec.headers.forEach((header,index)=>{let width=index<2?18:22;if(header.includes('Açıklama')||header.includes('Değerlendirmesi'))width=40;if(header.includes('Müşteri Adı'))width=34;sheet.getRange(`${colName(index+1)}:${colName(index+1)}`).format.columnWidth=width})
    const decisionIndex=spec.headers.indexOf('Müşteri Kararı')+1
    const explanationIndex=spec.headers.indexOf('Müşteri Açıklaması')+1
    if(decisionIndex>0){const a=colName(decisionIndex),b=colName(explanationIndex);sheet.getRange(`${a}6:${b}${lastRow}`).format.fill='#FFF2CC';sheet.getRange(`${a}6:${a}${lastRow}`).dataValidation={rule:{type:'list',values:spec.decisions}};sheet.getRange(`${a}6:${a}${lastRow}`).conditionalFormats.add('containsText',{text:'En yeni kayıt kullanılacak',format:{fill:'#D9EAD3',font:{color:'#274E13',bold:true}}})}
    const membershipIndex=spec.headers.indexOf('Üyelik Durumu')+1
    if(membershipIndex>0){const c=colName(membershipIndex);sheet.getRange(`${c}6:${c}${lastRow}`).conditionalFormats.add('containsText',{text:'bulunamadı',format:{fill:'#FCE5CD',font:{color:'#7F6000',bold:true}}});sheet.getRange(`${c}6:${c}${lastRow}`).conditionalFormats.add('containsText',{text:'belirsiz',format:{fill:'#FCE5CD',font:{color:'#7F6000',bold:true}}})}
    if(spec.name.startsWith('4 -')){sheet.getRange(`E6:F${lastRow}`).format.numberFormat='yyyy-mm-dd hh:mm:ss';sheet.getRange(`I6:I${lastRow}`).conditionalFormats.add('containsText',{text:'En yeni kayıt - kullanılacak',format:{fill:'#D9EAD3',font:{color:'#274E13',bold:true}}});sheet.getRange(`I6:I${lastRow}`).conditionalFormats.add('containsText',{text:'incelenecek',format:{fill:'#FCE5CD',font:{color:'#7F6000',bold:true}}})}
    sheet.getRange(`A6:D${lastRow}`).format.numberFormat='@'
    summaries[spec.name]={original:spec.original,removed:spec.removed,remaining:spec.rows.length,uncertain:spec.uncertain}
}

await fs.mkdir(outputDir,{recursive:true})
for(const spec of prepared){const preview=await workbook.render({sheetName:spec.name,range:`A1:${colName(spec.headers.length)}14`,scale:0.85,format:'png'});await fs.writeFile(path.join(outputDir,`preview-${spec.name.slice(0,1)}.png`),new Uint8Array(await preview.arrayBuffer()))}
for(const spec of prepared){const check=await workbook.inspect({kind:'table',range:`${spec.name}!A1:${colName(Math.min(spec.headers.length,10))}9`,include:'values,formulas',tableMaxRows:9,tableMaxCols:10});console.log(check.ndjson)}
const errors=await workbook.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:100},summary:'final formula error scan'});console.log(errors.ndjson)
const output=await SpreadsheetFile.exportXlsx(workbook);await output.save(outputPath)
console.log(JSON.stringify({outputPath,summaries}))
