import fs from 'node:fs/promises'
import path from 'node:path'
import { SpreadsheetFile, Workbook } from '@oai/artifact-tool'

const repo = 'D:/MGS/Repos/AssistFlow/AssistFlow-BE'
const snapshot = path.join(repo, 'tools/Collections.Snapshot/snapshots/mgs-inactive-20260919')
const reports = path.join(snapshot, 'reports')
const outputDir = path.join(repo, 'outputs/tahsilat-musteri-karar-listeleri-20260920')
const outputPath = path.join(outputDir, 'Tahsilat_Musteri_Karar_Listeleri.xlsx')

function parseLine(line) {
    const out = []; let current = ''; let quoted = false
    for (let i = 0; i < line.length; i++) {
        const c = line[i]
        if (c === '"') { if (quoted && line[i + 1] === '"') { current += '"'; i++ } else quoted = !quoted }
        else if (c === ';' && !quoted) { out.push(current); current = '' }
        else current += c
    }
    out.push(current); return out
}
async function readCsv(name) {
    const lines = (await fs.readFile(path.join(reports, name), 'utf8')).replace(/^\uFEFF/, '').split(/\r?\n/).filter(Boolean)
    const headers = parseLine(lines[0])
    return lines.slice(1).map(line => Object.fromEntries(headers.map((h, i) => [h, parseLine(line)[i] ?? ''])))
}
const customers = new Map()
for (const line of (await fs.readFile(path.join(snapshot, 'customers.ndjson'), 'utf8')).split(/\r?\n/)) {
    if (!line.trim()) continue
    const c = JSON.parse(line)
    customers.set(String(c.CustomerID ?? '').trim(), { name: String(c.Name ?? '').trim(), subscriber: String(c.SubscriberNo ?? '').trim() })
}
const customerRows = await readCsv('customer-exceptions.csv')
const q1 = await readCsv('question-1-contract-status.csv')
const q5 = await readCsv('question-5-process-type.csv')
const q6 = await readCsv('question-6-rate-conflicts.csv')
const q7 = await readCsv('rate-currency-amount-exceptions.csv')
const attachments = (await readCsv('contract-attachment-inventory.csv')).filter(r => r.PathStatus !== 'Dosya metadata yok')

const issueText = {
    CUSTOMER_TARGET_MISSING: 'AssistFlow’da aynı abone numarasıyla müşteri bulunamadı',
    CUSTOMER_ORPHAN: 'Sözleşmenin legacy müşteri kaydı bulunamadı',
    SUBSCRIBER_SOURCE_DUPLICATE: 'Aynı abone numarası birden fazla legacy müşteride bulunuyor',
    SUBSCRIBER_BLANK: 'Legacy müşteri kaydında abone numarası boş',
}
const related4 = new Set(Object.keys(issueText))
const specs = [
    {
        name: '1 - Sözleşme Durumu', title: 'Madde 1 - Sözleşme durumu boş veya belirtilmemiş kayıtlar',
        question: 'Bu sözleşmeler tahsilata dahil edilsin mi, yoksa aktarım dışında mı bırakılsın?',
        headers: ['Legacy Sözleşme No','Legacy Müşteri No','Abone No','Müşteri Adı','Sözleşme Durumu','Başlangıç Ay','Başlangıç Yıl','Bitiş Tarihi','Güncel Tutar','Para Birimi ID','Ödeme Dönemi ID','Müşteri Kararı','Müşteri Açıklaması','Teknik Referans'],
        rows: q1.map(r => [r.LegacyContractId,r.LegacyCustomerId,r.SubscriberNo,r.CustomerName,r.ContractStatusId || 'Boş',r.StartMonth,r.StartYear,r.EndDate,r.CurrentAmount,r.CurrencyId,r.PaymentTypeId,'','',r.IssueCodes]),
        decisionCol: 12, decisions: ['Tahsilata dahil et','Aktarım dışında bırak','İncelenecek'], inputStart: 12, inputEnd: 13,
    },
    {
        name: '2 - Müşteri Türü', title: 'Madde 2 - Legacy ve AssistFlow müşteri türü uyuşmayan kayıtlar',
        question: 'Bu müşterilerin türü AssistFlow’da Grup Üyesi olarak değiştirilsin mi?',
        headers: ['Legacy Sözleşme No','Legacy Müşteri No','Abone No','Müşteri Adı','AssistFlow Müşteri ID','Legacy Tür','Beklenen Tür','AssistFlow Tür','Müşteri Kararı','Doğru Müşteri Türü','Müşteri Açıklaması','Teknik Referans'],
        rows: customerRows.filter(r => r.IssueCodes.split(',').includes('CUSTOMER_TYPE_MISMATCH')).map(r => { const c=customers.get(r.LegacyCustomerId.trim())||{}; return [r.LegacyContractId,r.LegacyCustomerId,r.SubscriberNo||c.subscriber||'',c.name||'',r.TargetCustomerId,r.SourceCustomerType,r.ExpectedCustomerType,r.ActualCustomerType,'','','',r.IssueCodes] }),
        decisionCol: 9, decisions: ['Müşteri türünü değiştir','Mevcut türü koru','İncelenecek'], inputStart: 9, inputEnd: 11,
        extraValidation: { col: 10, values: ['N - Bireysel','GM - Grup Üyesi','G - Grup-Kurumsal','A - Cari-Fatura','BANKA'] },
    },
    {
        name: '3 - Bulunamayan Müşteri', title: 'Madde 3 - AssistFlow’da aynı abone numarasıyla bulunamayan müşteriler',
        question: 'Yeni müşteri oluşturulsun mu, mevcut müşteriyle eşleştirilsin mi, yoksa aktarım dışında mı bırakılsın?',
        headers: ['Legacy Sözleşme No','Legacy Müşteri No','Legacy Müşteri Adı','Legacy Abone No','Bulunan AssistFlow Müşteri ID','Sorun Açıklaması','Müşteri Kararı','Doğru SubscriberCode','Doğru AssistFlow Müşteri ID','Doğru Müşteri Adı','Müşteri Açıklaması','Teknik Referans'],
        rows: customerRows.filter(r => r.IssueCodes.split(',').includes('CUSTOMER_TARGET_MISSING')).map(r => { const c=customers.get(r.LegacyCustomerId.trim())||{}; return [r.LegacyContractId,r.LegacyCustomerId,c.name||'',r.SubscriberNo||c.subscriber||'',r.TargetCustomerId,issueText.CUSTOMER_TARGET_MISSING,'','','','','',r.IssueCodes] }),
        decisionCol: 7, decisions: ['Yeni müşteri oluştur','Mevcut müşteriyle eşleştir','Aktarım dışında bırak','İncelenecek'], inputStart: 7, inputEnd: 11,
    },
    {
        name: '4 - Müşteri Eşleştirme', title: 'Madde 4 - Abone numarası veya müşteri eşleşmesi sorunlu kayıtlar',
        question: 'Doğru müşteri eşleşmesini paylaşın; eşleştirilemeyen kayıtların aktarım durumunu belirtin.',
        headers: ['Legacy Sözleşme No','Legacy Müşteri No','Legacy Müşteri Adı','Legacy Abone No','Bulunan AssistFlow Müşteri ID','Sorun Açıklaması','Müşteri Kararı','Doğru SubscriberCode','Doğru AssistFlow Müşteri ID','Doğru Müşteri Adı','Müşteri Açıklaması','Teknik Referans'],
        rows: customerRows.map(r => { const codes=r.IssueCodes.split(',').filter(x=>related4.has(x)); if(!codes.length) return null; const c=customers.get(r.LegacyCustomerId.trim())||{}; return [r.LegacyContractId,r.LegacyCustomerId,c.name||'',r.SubscriberNo||c.subscriber||'',r.TargetCustomerId,codes.map(x=>issueText[x]).join('; '),'','','','','',codes.join(',')] }).filter(Boolean),
        decisionCol: 7, decisions: ['Mevcut müşteriyle eşleştir','Yeni müşteri oluştur','Aktarım dışında bırak','İncelenecek'], inputStart: 7, inputEnd: 11,
    },
    {
        name: '5 - Null İşlem Türü', title: 'Madde 5 - İşlem türü belirlenemeyen sözleşme tarihçeleri',
        question: 'Bu kayıtlar normal ücretli dönem mi kabul edilsin, yoksa aktarım dışında mı bırakılsın?',
        headers: ['Legacy Tarihçe No','Legacy Sözleşme No','Legacy Müşteri No','Abone No','Müşteri Adı','Başlangıç Ay','Başlangıç Yıl','Bitiş Tarihi','Tutar','Para Birimi ID','Ödeme Dönemi ID','Legacy İşlem Türü','Açıklama','Müşteri Kararı','Müşteri Açıklaması','Teknik Referans'],
        rows: q5.map(r => [r.LegacyHistoryId,r.LegacyContractId,r.LegacyCustomerId,r.SubscriberNo,r.CustomerName,r.StartMonth,r.StartYear,r.EndDate,r.Amount,r.CurrencyId,r.PaymentTypeId,r.ProcessType,r.Description,'','',r.IssueCodes]),
        decisionCol: 14, decisions: ['Normal ücretli dönem','Ücretsiz dönem','Hizmet dondurma','Aktarım dışında bırak','İncelenecek'], inputStart: 14, inputEnd: 15,
    },
    {
        name: '6 - Tarife Çakışmaları', title: 'Madde 6 - Tarife çakışması veya güncel tutar uyumsuzluğu bulunan kayıtlar',
        question: 'Doğru tarife bilgisini ve hangi kaydın esas alınacağını belirtin.',
        headers: ['Sorun Türü','Legacy Sözleşme No','Legacy Tarihçe No','Legacy Müşteri No','Abone No','Müşteri Adı','Başlangıç Ay','Başlangıç Yıl','Bitiş Tarihi','Tutar','Para Birimi ID','Ödeme Dönemi ID','İşlem Türü','Legacy Açıklama','Müşteri Kararı','Doğru Başlangıç','Doğru Bitiş','Doğru Tutar','Müşteri Açıklaması','Teknik Referans'],
        rows: q6.map(r => [r.ProblemType,r.LegacyContractId,r.LegacyHistoryId,r.LegacyCustomerId,r.SubscriberNo,r.CustomerName,r.StartMonth,r.StartYear,r.EndDate,r.Amount,r.CurrencyId,r.PaymentTypeId,r.ProcessType,r.Description,'','','','','',r.IssueCodes]),
        decisionCol: 15, decisions: ['Güncel sözleşmeyi esas al','Son tarihçeyi esas al','Kayıtları manuel düzelt','Aktarım dışında bırak','İncelenecek'], inputStart: 15, inputEnd: 19,
    },
    {
        name: '7 - Para Birimi Tutar', title: 'Madde 7 - Para birimi veya tutarı belirlenemeyen tarife kayıtları',
        question: 'Doğru para birimi ve dönem tutarını paylaşın; belirlenemeyen kaydın aktarım durumunu belirtin.',
        headers: ['Legacy Tarihçe No','Legacy Sözleşme No','Legacy Müşteri No','Abone No','Müşteri Adı','Başlangıç Ay','Başlangıç Yıl','Bitiş Tarihi','Ödeme Dönemi ID','Legacy Para Birimi ID','Legacy Tutar','İşlem Türü','Legacy Açıklama','Sorun Açıklaması','Müşteri Kararı','Doğru Para Birimi','Diğer Para Birimi','Doğru Dönem Tutarı','Müşteri Açıklaması','Teknik Referans'],
        rows: q7.map(r => [r.LegacyContractHistoryId,r.LegacyContractId,r.LegacyCustomerId,r.SubscriberNo,r.CustomerName,r.StartMonth,r.StartYear,r.EndDate,r.PaymentTypeId,r.RawCurrencyId,r.RawAmount,r.ProcessType,r.Description,r.IssueCodes.includes('AMOUNT_INVALID')?'Para birimi ve/veya tutar belirlenemiyor':'Para birimi belirlenemiyor','','','','','',r.IssueCodes]),
        decisionCol: 15, decisions: ['Bilgiyi tamamla','Aktarım dışında bırak','İncelenecek'], inputStart: 15, inputEnd: 19,
        extraValidation: { col: 16, values: ['TRY','USD','EUR','GBP','Diğer'] },
    },
    {
        name: '8 - Sözleşme Dosyaları', title: 'Madde 8 - Dosya kaydı bulunan sözleşmeler',
        question: 'Fiziksel dosyası bulunamayan kayıtlar dosyasız aktarılsın mı? Paylaşılacak dosyalar için referans ekleyin.',
        headers: ['Legacy Sözleşme No','AssistFlow Sözleşme ID','Abone No','Dosya Adı','Legacy Dosya Yolu','Uzantı','Yol Durumu','Uzantı Durumu','Aynı Yolu Kullanan Kayıt','Fiziksel Dosya Durumu','Müşteri Kararı','Paylaşılacak Dosya / Referans','Müşteri Açıklaması'],
        rows: attachments.map(r => [r.LegacyContractId,r.TargetContractId,r.SubscriberNo,r.AttachmentName,r.LegacyPath,r.Extension,r.PathStatus,r.ExtensionStatus,r.DuplicatePathCount,r.PhysicalFileStatus,'','','']),
        decisionCol: 11, decisions: ['Dosyayı paylaşacağız','Dosyasız aktar','Aktarım dışında bırak','İncelenecek'], inputStart: 11, inputEnd: 13,
    },
]

function colName(n) { let s=''; while(n){ n--; s=String.fromCharCode(65+n%26)+s; n=Math.floor(n/26) } return s }
const workbook = Workbook.create()
for (let index=0; index<specs.length; index++) {
    const s=specs[index]; const ws=workbook.worksheets.add(s.name); ws.showGridLines=false; ws.tabColor=index%2?'#5B9BD5':'#1F4E78'
    const lastCol=colName(s.headers.length); const lastRow=s.rows.length+4
    ws.getRange(`A1:${lastCol}1`).merge(); ws.getRange('A1').values=[[s.title]]
    ws.getRange(`A1:${lastCol}1`).format={fill:'#1F4E78',font:{name:'Arial',size:15,bold:true,color:'#FFFFFF'},rowHeight:28,verticalAlignment:'center'}
    ws.getRange(`A2:${lastCol}2`).merge(); ws.getRange('A2').values=[[`${s.question} Toplam ${s.rows.length.toLocaleString('tr-TR')} kayıt.`]]
    ws.getRange(`A2:${lastCol}2`).format={fill:'#D9EAF7',font:{name:'Arial',size:10,italic:true,color:'#1F1F1F'},wrapText:true,rowHeight:30,verticalAlignment:'center'}
    ws.getRange(`A4:${lastCol}4`).values=[s.headers]
    if(s.rows.length) ws.getRange(`A5:${lastCol}${lastRow}`).values=s.rows
    const table=ws.tables.add(`A4:${lastCol}${lastRow}`,true,`Madde${index+1}Tablosu`); table.style='TableStyleMedium2'; table.showFilterButton=true
    ws.freezePanes.freezeRows(4); ws.freezePanes.freezeColumns(2)
    ws.getRange(`A4:${lastCol}${lastRow}`).format.font={name:'Arial',size:9}
    ws.getRange(`A4:${lastCol}4`).format={fill:'#1F4E78',font:{name:'Arial',size:9,bold:true,color:'#FFFFFF'},wrapText:true,horizontalAlignment:'center',verticalAlignment:'center',rowHeight:42}
    const inputStart=colName(s.inputStart), inputEnd=colName(s.inputEnd), decision=colName(s.decisionCol)
    ws.getRange(`${inputStart}5:${inputEnd}${lastRow}`).format.fill='#FFF2CC'
    ws.getRange(`${decision}5:${decision}${lastRow}`).dataValidation={rule:{type:'list',values:s.decisions}}
    if(s.extraValidation){ const c=colName(s.extraValidation.col); ws.getRange(`${c}5:${c}${lastRow}`).dataValidation={rule:{type:'list',values:s.extraValidation.values}} }
    ws.getRange(`${decision}5:${decision}${lastRow}`).conditionalFormats.add('containsText',{text:'Aktarım dışında bırak',format:{fill:'#F4CCCC',font:{color:'#9C0006',bold:true}}})
    ws.getRange(`${decision}5:${decision}${lastRow}`).conditionalFormats.add('containsText',{text:'İncelenecek',format:{fill:'#FCE5CD',font:{color:'#7F6000',bold:true}}})
    ws.getRange(`A5:${lastCol}${lastRow}`).format.verticalAlignment='top'
    for(let c=1;c<=s.headers.length;c++) ws.getRange(`${colName(c)}:${colName(c)}`).format.columnWidth = c<=2?18:(s.headers[c-1].includes('Açıklama')||s.headers[c-1].includes('Yolu')?38:22)
    ws.getRange(`${decision}:${decision}`).format.columnWidth=28
    ws.getRange(`A5:${lastCol}${lastRow}`).format.wrapText=true
    ws.getRange(`A5:E${lastRow}`).format.numberFormat='@'
}

await fs.mkdir(outputDir,{recursive:true})
for(const s of specs){ const img=await workbook.render({sheetName:s.name,range:`A1:${colName(Math.min(s.headers.length,12))}12`,scale:0.9,format:'png'}); await fs.writeFile(path.join(outputDir,`preview-${s.name.slice(0,1)}.png`),new Uint8Array(await img.arrayBuffer())) }
for(const s of specs){ const check=await workbook.inspect({kind:'table',range:`${s.name}!A1:${colName(Math.min(s.headers.length,8))}7`,include:'values,formulas',tableMaxRows:7,tableMaxCols:8}); console.log(check.ndjson) }
const errors=await workbook.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:100},summary:'final formula error scan'}); console.log(errors.ndjson)
const output=await SpreadsheetFile.exportXlsx(workbook); await output.save(outputPath)
console.log(JSON.stringify({outputPath,counts:Object.fromEntries(specs.map((s,i)=>[i+1,s.rows.length]))}))
