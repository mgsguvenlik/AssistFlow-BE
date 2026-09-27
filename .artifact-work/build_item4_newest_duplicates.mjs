import fs from 'node:fs/promises'
import path from 'node:path'
import { FileBlob, SpreadsheetFile, Workbook } from '@oai/artifact-tool'

const sourcePath = 'C:/Users/Mehmet Zeki KARA/Downloads/Tahsilat_Musteri_Karar_Listeleri.xlsx'
const outputDir = 'D:/MGS/Repos/AssistFlow/AssistFlow-BE/outputs/tahsilat-madde-4-duplicate-20260923'
const outputPath = path.join(outputDir, 'Tahsilat_Madde_4_Guncel_Musteri_Eslestirme_Listesi.xlsx')
const normalize = value => String(value ?? '').trim()
const normalizeSubscriber = value => normalize(value).toLocaleUpperCase('tr-TR')
const colName = n => { let s=''; while(n){ n--; s=String.fromCharCode(65+n%26)+s; n=Math.floor(n/26) } return s }

const customers = new Map()
const groups = new Map()
for (const line of (await fs.readFile('.artifact-work/customer-created.csv', 'latin1')).split(/\r?\n/)) {
    if (!/^\s*\d+\s*\|/.test(line)) continue
    const [idRaw, subscriberRaw, , createdRaw] = line.split('|')
    const id = normalize(idRaw), subscriber = normalizeSubscriber(subscriberRaw), createdText = normalize(createdRaw)
    const createdTime = Date.parse(createdText)
    const customer = { id, subscriber, createdText, createdTime: Number.isFinite(createdTime) ? createdTime : null }
    customers.set(id, customer)
    if (subscriber) { if (!groups.has(subscriber)) groups.set(subscriber, []); groups.get(subscriber).push(customer) }
}

const source = await SpreadsheetFile.importXlsx(await FileBlob.load(sourcePath))
const sourceSheet = source.worksheets.getItem('4 - Müşteri Eşleştirme')
const sourceValues = sourceSheet.getUsedRange(true).values
const headerIndex = sourceValues.findIndex(r => r.some(c => normalize(c) === 'Legacy Sözleşme No'))
const sourceHeaders = sourceValues[headerIndex].map(normalize)
const index = Object.fromEntries(sourceHeaders.map((h, i) => [h, i]))
const sourceRows = sourceValues.slice(headerIndex + 1).filter(r => r.some(c => normalize(c)))

const rows = []
let removedOld = 0, retainedNewest = 0, unresolved = 0
for (const row of sourceRows) {
    const issueCodes = normalize(row[index['Teknik Referans']]).split(',').filter(Boolean)
    const isDuplicate = issueCodes.includes('SUBSCRIBER_SOURCE_DUPLICATE')
    const customerId = normalize(row[index['Legacy Müşteri No']])
    const customer = customers.get(customerId)
    let latestDate = null, duplicateStatus = 'Duplicate değil', customerDecision = normalize(row[index['Müşteri Kararı']])
    let technicalReference = normalize(row[index['Teknik Referans']])
    if (isDuplicate) {
        const subscriber = customer?.subscriber || normalizeSubscriber(row[index['Legacy Abone No']])
        const group = groups.get(subscriber) ?? []
        const dated = group.filter(c => c.createdTime !== null)
        const maxDate = dated.length ? Math.max(...dated.map(c => c.createdTime)) : null
        const newest = maxDate === null ? [] : dated.filter(c => c.createdTime === maxDate)
        latestDate = maxDate === null ? null : new Date(maxDate)
        if (!customer || customer.createdTime === null || newest.length !== 1) {
            unresolved++
            duplicateStatus = 'Tarih eşit veya eksik - incelenecek'
        } else if (customer.id === newest[0].id) {
            retainedNewest++
            duplicateStatus = 'En yeni kayıt - kullanılacak'
            customerDecision = 'En yeni kayıt kullanılacak'
            technicalReference = 'SUBSCRIBER_SOURCE_DUPLICATE_RESOLVED_NEWEST'
        } else {
            removedOld++
            continue
        }
    }
    rows.push([
        normalize(row[index['Legacy Sözleşme No']]), customerId,
        normalize(row[index['Legacy Müşteri Adı']]), normalize(row[index['Legacy Abone No']]),
        customer?.createdTime === null || customer?.createdTime === undefined ? null : new Date(customer.createdTime),
        latestDate, normalize(row[index['Bulunan AssistFlow Müşteri ID']]),
        normalize(row[index['Sorun Açıklaması']]), duplicateStatus, customerDecision,
        normalize(row[index['Doğru SubscriberCode']]), normalize(row[index['Doğru AssistFlow Müşteri ID']]),
        normalize(row[index['Doğru Müşteri Adı']]), normalize(row[index['Müşteri Açıklaması']]), technicalReference,
    ])
}

const workbook = Workbook.create()
const sheet = workbook.worksheets.add('4 - Müşteri Eşleştirme')
sheet.showGridLines = false
sheet.tabColor = '#1F4E78'
const headers = [
    'Legacy Sözleşme No','Legacy Müşteri No','Legacy Müşteri Adı','Legacy Abone No',
    'Müşteri Kayıt Tarihi','Aynı Abone No En Yeni Tarih','Bulunan AssistFlow Müşteri ID',
    'Sorun Açıklaması','Duplicate Değerlendirmesi','Müşteri Kararı','Doğru SubscriberCode',
    'Doğru AssistFlow Müşteri ID','Doğru Müşteri Adı','Müşteri Açıklaması','Teknik Referans',
]
sheet.getRange('A1:O1').merge(); sheet.getRange('A1').values=[['Madde 4 - Güncel müşteri eşleştirme listesi']]
sheet.getRange('A1:O1').format={fill:'#1F4E78',font:{name:'Arial',size:15,bold:true,color:'#FFFFFF'},rowHeight:28,verticalAlignment:'center'}
sheet.getRange('A2:O2').merge(); sheet.getRange('A2').values=[[
    `Aynı abone numarasındaki eski müşteri kayıtları çıkarıldı. ${removedOld.toLocaleString('tr-TR')} eski satır çıkarıldı, ${retainedNewest.toLocaleString('tr-TR')} en yeni satır kullanılacak, ${unresolved.toLocaleString('tr-TR')} satır tarih eşitliği veya eksikliği nedeniyle incelenecek.`,
]]
sheet.getRange('A2:O2').format={fill:'#D9EAF7',font:{name:'Arial',size:10,italic:true,color:'#1F1F1F'},wrapText:true,rowHeight:34,verticalAlignment:'center'}
sheet.getRange('A3:O3').merge(); sheet.getRange('A3').values=[[
    `Önceki liste: ${sourceRows.length.toLocaleString('tr-TR')} satır. Güncel liste: ${rows.length.toLocaleString('tr-TR')} satır. Sarı alanlar yalnız inceleme gereken kayıtlar için doldurulacaktır.`,
]]
sheet.getRange('A3:O3').format={font:{name:'Arial',size:10,color:'#595959'},wrapText:true,rowHeight:26,verticalAlignment:'center'}
sheet.getRange('A5:O5').values=[headers]
const lastRow=rows.length+5
sheet.getRange(`A6:O${lastRow}`).values=rows
const table=sheet.tables.add(`A5:O${lastRow}`,true,'Madde4GuncelTablosu'); table.style='TableStyleMedium2'; table.showFilterButton=true
sheet.freezePanes.freezeRows(5); sheet.freezePanes.freezeColumns(2)
sheet.getRange(`A5:O${lastRow}`).format.font={name:'Arial',size:9}
sheet.getRange('A5:O5').format={fill:'#1F4E78',font:{name:'Arial',size:9,bold:true,color:'#FFFFFF'},wrapText:true,horizontalAlignment:'center',verticalAlignment:'center',rowHeight:44}
sheet.getRange(`J6:N${lastRow}`).format.fill='#FFF2CC'
sheet.getRange(`E6:F${lastRow}`).format.numberFormat='yyyy-mm-dd hh:mm:ss'
sheet.getRange(`A6:D${lastRow}`).format.numberFormat='@'
sheet.getRange(`G6:G${lastRow}`).format.numberFormat='@'
sheet.getRange(`K6:L${lastRow}`).format.numberFormat='@'
sheet.getRange(`A6:O${lastRow}`).format.verticalAlignment='top'
sheet.getRange(`C6:C${lastRow}`).format.wrapText=true
sheet.getRange(`H6:I${lastRow}`).format.wrapText=true
sheet.getRange(`N6:N${lastRow}`).format.wrapText=true
sheet.getRange(`J6:J${lastRow}`).dataValidation={rule:{type:'list',values:['Mevcut müşteriyle eşleştir','Yeni müşteri oluştur','Aktarım dışı bırak','İncelenecek','En yeni kayıt kullanılacak']}}
sheet.getRange(`I6:I${lastRow}`).conditionalFormats.add('containsText',{text:'En yeni kayıt - kullanılacak',format:{fill:'#D9EAD3',font:{color:'#274E13',bold:true}}})
sheet.getRange(`I6:I${lastRow}`).conditionalFormats.add('containsText',{text:'incelenecek',format:{fill:'#FCE5CD',font:{color:'#7F6000',bold:true}}})
sheet.getRange(`J6:J${lastRow}`).conditionalFormats.add('containsText',{text:'En yeni kayıt kullanılacak',format:{fill:'#D9EAD3',font:{color:'#274E13',bold:true}}})
const widths=[18,18,34,20,22,24,23,46,34,28,22,25,34,42,44]
widths.forEach((width,i)=>sheet.getRange(`${colName(i+1)}:${colName(i+1)}`).format.columnWidth=width)

await fs.mkdir(outputDir,{recursive:true})
const preview=await workbook.render({sheetName:'4 - Müşteri Eşleştirme',range:'A1:O14',scale:0.9,format:'png'})
await fs.writeFile(path.join(outputDir,'preview.png'),new Uint8Array(await preview.arrayBuffer()))
const inspect=await workbook.inspect({kind:'table',range:'4 - Müşteri Eşleştirme!A1:O12',include:'values,formulas',tableMaxRows:12,tableMaxCols:15}); console.log(inspect.ndjson)
const errors=await workbook.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:100},summary:'final formula error scan'}); console.log(errors.ndjson)
const output=await SpreadsheetFile.exportXlsx(workbook); await output.save(outputPath)
console.log(JSON.stringify({outputPath,sourceRows:sourceRows.length,removedOld,retainedNewest,unresolved,remainingRows:rows.length}))
