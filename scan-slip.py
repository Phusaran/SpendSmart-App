import cv2
import pytesseract
from flask import Flask, request, jsonify, render_template_string
import numpy as np
import base64
import traceback
import re
import difflib
from google import genai
import os
import json

app = Flask(__name__)
client = genai.Client(api_key="api_key_here")
# ---------------------------------------------------------
# 🛠️ CONFIGURATION
# ---------------------------------------------------------
pytesseract.pytesseract.tesseract_cmd = r'C:\Program Files\Tesseract-OCR\tesseract.exe'

# รายชื่อเดือนย่อไทยสำหรับทำ Fuzzy Matching
VALID_MONTHS = ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."]

def get_base64_img(img):
    _, buffer = cv2.imencode('.jpg', img)
    return base64.b64encode(buffer).decode('utf-8')

def clean_kbank_date(ocr_text):
    """
    ฟังก์ชันทำความสะอาดข้อมูลวันที่: แก้ปัญหา พ.ด. -> พ.ค. และปี 69
    """
    # 1. ทำความสะอาดข้อความเบื้องต้น
    text_nospace = ocr_text.replace(" ", "")
    
    # 2. Regex: จับแค่ (วัน 1-2 หลัก) (ตัวหนังสือเดือน) (ปี 2 หลัก)
    match = re.search(r'(\d{1,2})(.*?)(\d{2})', text_nospace)
    
    if match:
        day = match.group(1)
        raw_month = match.group(2)
        year = match.group(3)

        # 🛠️ [Fix] แก้ปัญหาปีเพี้ยน (69 vs 09) อิงจากปีปัจจุบัน 2569
        if year in ["09", "89", "05"]:
            year = "69"

        # 🛠️ [Fix] แปลงตัวอักษรที่ AI มักอ่านผิดให้เป็น "ค" หรือ "พ" ก่อนเดาเดือน
        # เพิ่ม 'ด' -> 'ค' ตามที่คุณแจ้งมาครับ
        raw_month = raw_month.replace('w', 'พ').replace('W', 'พ').replace('A', 'ค').replace('a', 'ค').replace('ด', 'ค')
        
        # 3. Fuzzy Match หาเดือนที่ใกล้เคียงที่สุด
        closest = difflib.get_close_matches(raw_month, VALID_MONTHS, n=1, cutoff=0.1)
        final_month = closest[0] if closest else "ม.ค."

        # Double Check สำหรับเดือนพฤษภาคม (พ.ค.) โดยเฉพาะ
        if final_month == "พ.ย." and ("ค" in raw_month or "ด" in raw_month):
            final_month = "พ.ค."

        return f"{day} {final_month} {year}"
    
    return ocr_text

# ---------------------------------------------------------
# 🌐 WEB UI (Debug Mode)
# ---------------------------------------------------------
@app.route('/', methods=['GET'])
def index():
    return '''
    <!doctype html>
    <title>SpendSmart AI v5</title>
    <body style="font-family: Arial; padding: 20px; background: #121212; color: white;">
        <h2>🛠️ SpendSmart AI OCR (Manual Correction Version)</h2>
        <p>แก้ไขปัญหา พ.ด. เป็น พ.ค. และตัดส่วนเวลาออกเพื่อความนิ่ง</p>
        <form action="/debug-scan" method="post" enctype="multipart/form-data">
          <input type="file" name="image" accept="image/*" style="color: white;"><br><br>
          <input type="submit" value="🔍 สแกนทดสอบ" style="padding: 10px 20px; background: #D4AF37; border: none; border-radius: 5px; cursor: pointer; font-weight: bold; color: white;">
        </form>
    </body>
    '''
@app.route('/analyze-spending', methods=['POST'])
def analyze_spending():
    try:
        data = request.json
        print("💡 [1] ได้รับข้อมูลจากแอป:", data)

        total = data.get('total', 0)
        categories = data.get('categories', [])

        prompt = f"""
        คุณคือผู้เชี่ยวชาญด้านการเงินส่วนบุคคลที่คอยให้คำแนะนำอย่างเป็นกันเองและเข้าใจง่าย
        นี่คือข้อมูลการใช้จ่ายของฉันในเดือนนี้:
        - ยอดรวมทั้งหมด: {total} บาท
        - รายละเอียดแต่ละหมวด: {categories}

        ช่วยวิเคราะห์พฤติกรรมการใช้จ่ายของฉันให้หน่อย:
        1. หมวดไหนที่ฉันใช้เงินเปลืองเกินไปไหม?
        2. ขอคำแนะนำสั้นๆ 2-3 ข้อเพื่อประหยัดเงินในเดือนหน้า
        ตอบเป็นภาษาไทย สั้นๆ กระชับ และเป็นธรรมชาติ
        """

        print("🚀 [2] กำลังส่งข้อมูลให้ Gemini...")
        response = client.models.generate_content(
            model='gemini-2.5-flash',
            contents=prompt
        )
        print("✅ [3] Gemini ตอบกลับมาแล้ว!")
        
        return jsonify({
            "status": "success",
            "advice": response.text
        })
    except Exception as e:
        print("🔥 [ERROR] เกิดข้อผิดพลาดที่ Python:")
        traceback.print_exc() # บรรทัดนี้จะปริ้นท์ Error สีแดงๆ ออกมาบอกสาเหตุที่แท้จริง
        return jsonify({"status": "error", "message": str(e)}), 500
@app.route('/chat', methods=['POST'])
def chat_with_ai():
    try:
        data = request.json
        user_message = data.get('message', '')
        financial_context = data.get('context', '') # สรุปการใช้จ่ายที่แอปมือถือส่งมาให้

        # สร้าง Prompt แบบสวมบทบาทให้ AI
        prompt = f"""
        คุณคือ SpendSmart AI ผู้ช่วยส่วนตัวด้านการเงินที่ฉลาด เป็นกันเอง และให้กำลังใจเก่ง
        นี่คือข้อมูลการใช้จ่ายของผู้ใช้ในเดือนนี้:
        {financial_context}

        ผู้ใช้พิมพ์ถาม/คุยกับคุณว่า: "{user_message}"
        
        กรุณาตอบกลับเป็นภาษาไทย สั้นๆ กระชับ ตอบให้ตรงคำถาม และให้คำแนะนำที่นำไปใช้ได้จริง 
        (ถ้าผู้ใช้ทักทายเฉยๆ ก็ให้ทักทายกลับและถามว่ามีอะไรให้ช่วยเรื่องการเงินไหม)
        """

        response = client.models.generate_content(
            model='gemini-2.5-flash',
            contents=prompt
        )
        
        return jsonify({
            "status": "success",
            "reply": response.text
        })
    except Exception as e:
        traceback.print_exc()
        return jsonify({"status": "error", "message": str(e)}), 500
@app.route('/debug-scan', methods=['POST'])
def debug_scan():
    try:
        file = request.files['image']
        img_array = np.frombuffer(file.read(), np.uint8)
        img = cv2.imdecode(img_array, cv2.IMREAD_COLOR)
        img_resized = cv2.resize(img, (1080, 1920))

        # พิกัดวันที่ [Y:Y, X:X]
        date_crop = img_resized[132:234, 48:470]
        # พิกัดยอดเงิน
        amount_crop = img_resized[1520:1602, 380:554]

        # --- ยอดเงิน ---
        gray_amount = cv2.cvtColor(amount_crop, cv2.COLOR_BGR2GRAY)
        _, thresh_amount = cv2.threshold(gray_amount, 150, 255, cv2.THRESH_BINARY_INV)
        amount_text = pytesseract.image_to_string(thresh_amount, config='--psm 7 -c tessedit_char_whitelist=0123456789.').strip()

        # --- วันที่ ---
        gray_date = cv2.cvtColor(date_crop, cv2.COLOR_BGR2GRAY)
        gray_date = cv2.resize(gray_date, None, fx=2, fy=2, interpolation=cv2.INTER_CUBIC)
        gray_date = cv2.medianBlur(gray_date, 3)
        _, thresh_date = cv2.threshold(gray_date, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU)
        
        raw_date = pytesseract.image_to_string(thresh_date, lang='tha', config='--psm 6').strip()
        date_text = clean_kbank_date(raw_date)

        html = f'''
        <body style="font-family: Arial; padding: 20px; background: #121212; color: white;">
            <h2>📊 ผลการวิเคราะห์ (Corrected)</h2>
            <div style="background: #1E1E1E; padding: 20px; border-radius: 15px; margin-bottom: 20px; border-left: 5px solid #00E5FF;">
                <h3>💰 ยอดเงิน: <span style="color: #00E5FF;">{amount_text}</span></h3>
            </div>
            <div style="background: #1E1E1E; padding: 20px; border-radius: 15px; border-left: 5px solid #D4AF37;">
                <h3>📅 วันที่: <span style="color: #D4AF37;">{date_text}</span></h3>
                <p style="color: #666; font-size: 11px;">ค่าดิบก่อนคลีน: {raw_date}</p>
            </div>
            <br><a href="/" style="color: #666;">⬅️ กลับ</a>
        </body>
        '''
        return render_template_string(html)
    except Exception as e:
        traceback.print_exc()
        return f"Error: {str(e)}"

# ---------------------------------------------------------
# 📱 API สำหรับแอป .NET MAUI
# ---------------------------------------------------------
@app.route('/voice-to-expense', methods=['POST'])
def voice_to_expense():
    try:
        audio_file = request.files['audio']
        temp_path = "temp_audio.wav"
        audio_file.save(temp_path)

        # 1. อัปโหลดไฟล์เสียงไปให้ Gemini
        uploaded_file = client.files.upload(file=temp_path)

        prompt = """
        ฟังเสียงนี้แล้วสกัดข้อมูลรายจ่าย/รายรับ ออกมาเป็น JSON เท่านั้น
        รูปแบบ: {"amount": ตัวเลข, "type": "Expense" หรือ "Income", "subCategory": "หมวดหมู่สั้นๆ", "note": "รายละเอียด"}
        เช่น {"amount": 350, "type": "Expense", "subCategory": "รถยนต์", "note": "เปลี่ยนสายกีตาร์"}
        ตอบแค่ JSON ห้ามมีข้อความอื่นหรือ markdown (ไม่ต้องมี ```json )
        """

        # 2. ให้ Gemini วิเคราะห์
        response = client.models.generate_content(
            model='gemini-2.5-flash',
            contents=[uploaded_file, prompt]
        )
        
        # 3. ล้างไฟล์ขยะ
        os.remove(temp_path)
        client.files.delete(name=uploaded_file.name)

        # 4. จัดการข้อความที่ได้มาให้อยู่ในรูป JSON
        raw_text = response.text.strip()
        if raw_text.startswith("```json"):
            raw_text = raw_text[7:-3]
        elif raw_text.startswith("```"):
            raw_text = raw_text[3:-3]
            
        result = json.loads(raw_text)
        
        return jsonify({
            "status": "success",
            "data": result
        })

    except Exception as e:
        traceback.print_exc()
        return jsonify({"status": "error", "message": str(e)}), 500
@app.route('/scan-slip', methods=['POST'])
def scan_slip_api():
    try:
        file = request.files['image']
        img_array = np.frombuffer(file.read(), np.uint8)
        img = cv2.imdecode(img_array, cv2.IMREAD_COLOR)
        img_resized = cv2.resize(img, (1080, 1920))

        # สกัดยอดเงิน
        amount_crop = img_resized[1520:1602, 380:554]
        gray_amount = cv2.cvtColor(amount_crop, cv2.COLOR_BGR2GRAY)
        _, thresh_amount = cv2.threshold(gray_amount, 150, 255, cv2.THRESH_BINARY_INV)
        amount_text = pytesseract.image_to_string(thresh_amount, config='--psm 7 -c tessedit_char_whitelist=0123456789.').strip()

        # สกัดวันที่
        date_crop = img_resized[132:234, 48:470]
        gray_date = cv2.cvtColor(date_crop, cv2.COLOR_BGR2GRAY)
        gray_date = cv2.resize(gray_date, None, fx=2, fy=2, interpolation=cv2.INTER_CUBIC)
        gray_date = cv2.medianBlur(gray_date, 3)
        _, thresh_date = cv2.threshold(gray_date, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU)
        raw_date = pytesseract.image_to_string(thresh_date, lang='tha', config='--psm 6').strip()
        date_text = clean_kbank_date(raw_date)

        return jsonify({
            "status": "success",
            "amount": amount_text if amount_text else "0",
            "date": date_text
        })
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)