import fs from 'node:fs/promises'
import crypto from 'node:crypto'
import { SpreadsheetFile } from '@oai/artifact-tool'

const sourcePath = 'C:/Users/Mehmet Zeki KARA/Downloads/24.09.2026 Kopya Tahsilat_Guncel_Karar_Listeleri_1_4_5_6_7.xlsx'
const bytes = await fs.readFile(sourcePath)
const hash = crypto.createHash('sha256').update(bytes).digest('hex')
const workbook = await SpreadsheetFile.importXlsx(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength))
console.log((await workbook.inspect({kind:'sheet',include:'id,name',maxChars:2500})).ndjson)
const trim = v => String(v ?? '').trim()
const sheets = []
for (const name of ['5 - Null İşlem Türü','6 - Tarife Çakışmaları','7 - Para Birimi Tutar']) {
    const sheet = workbook.worksheets.getItem(name)
    const values = sheet.getUsedRange().values
    const headerIndex = values.findIndex(r => r.some(c => trim(c)==='Legacy Sözleşme No'))
    if(headerIndex<0) throw Error(`Başlık yok: ${name}`)
    const headers = values[headerIndex].map(trim)
    const rows = values.slice(headerIndex+1).map((row,i) => ({excelRow:headerIndex+i+2,...Object.fromEntries(headers.map((h,j)=>[h,row[j]]))}))
        .filter(r=>trim(r['Legacy Sözleşme No']) || trim(r['Legacy Tarihçe No']))
    const noteKey = headers.find(h=>h.toLocaleUpperCase('tr-TR').includes('TAHSİLAT')&&h.toLocaleUpperCase('tr-TR').includes('AÇIKLAMA'))
    if(!noteKey) throw Error(`Tahsilat açıklama sütunu yok: ${name}`)
    const notes = new Map()
    for(const row of rows){const note=trim(row[noteKey])||'(YANIT BOŞ)'; const group=notes.get(note)||[]; group.push(row.excelRow);notes.set(note,group)}
    sheets.push({name,headerIndex,headers,noteKey,rows})
    console.log(JSON.stringify({name,headers,count:rows.length,notes:[...notes].map(([note,rows])=>({note,count:rows.length,rows}))}))
}
await fs.writeFile('.tools/items567-workbook.json', JSON.stringify({sourcePath,hash,sheets},null,2))
const ids = field => [...new Set(sheets.flatMap(s=>s.rows.map(r=>trim(r[field]))).filter(Boolean))].map(v=>{if(!/^\d+$/.test(v))throw Error(`Geçersiz kimlik ${v}`);return v})
await fs.writeFile('.tools/items567-parameters.json',JSON.stringify({ContractIds:JSON.stringify(ids('Legacy Sözleşme No')),HistoryIds:JSON.stringify(ids('Legacy Tarihçe No'))}))
console.log('Entity counts:',JSON.stringify({contracts:ids('Legacy Sözleşme No').length,histories:ids('Legacy Tarihçe No').length}))
for(const s of sheets){const correctionColumns=s.headers.filter(h=>/^(Doğru|Diğer|Müşteri Kararı|Müşteri Açıklaması)/.test(h));console.log('Corrections',s.name,JSON.stringify(s.rows.filter(r=>correctionColumns.some(c=>trim(r[c]))).map(r=>({row:r.excelRow,...Object.fromEntries(correctionColumns.map(c=>[c,r[c]]))}))))}
console.log('SHA256',hash)
