import fs from 'fs';
import pdf from 'pdf-parse';

let dataBuffer = fs.readFileSync('d:\\CODE\\revit-mcp\\1.BaiTapMau_Dien.pdf');

pdf(dataBuffer).then(function(data) {
    console.log(data.text);
}).catch(function(error){
    console.error("Error parsing pdf", error);
});
