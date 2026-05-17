"""
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ระบบหลังบ้าน app.py - เวอร์ชัน Clean Up]
ไฟล์นี้คือ "เซิร์ฟเวอร์สมองกลส่วนกลาง (AI & OCR Web API)" ของแอป SpendSmart พัฒนาด้วย Python Flask
ทำหน้าที่เปิด Endpoint (ประตูรับส่งข้อมูล) ผ่านระบบเครือข่าย เพื่อให้แอปพลิเคชันมือถือ (.NET MAUI) 
ส่งข้อมูลดิบมาให้ประมวลผล โดยมีความสามารถหลัก 3 ส่วนหลังจากปรับปรุงระบบ (Clean Up) คือ:

1. ระบบสแกนและวิเคราะห์สลิป (Slip OCR Processing): ใช้ OpenCV ครอบตัดพิกัด (Crop) ยอดเงินและวันที่ 
   ของสลิปธนาคาร (กสิกรไทย) นำมาปรับความคมชัด (Preprocessing) และใช้ Tesseract OCR แกะตัวหนังสือออกมา
2. ระบบลอจิกทำความสะอาดวันที่ (Fuzzy Thai Date Cleaning): แก้ปัญหาคลาสสิกของภาษาไทย โดยใช้ Regex 
   และอัลกอริทึม Fuzzy Matching (difflib) แก้ไขตัวอักษรที่ OCR มักอ่านพลาด (เช่น พ.ด. หรือ พ.ย. ให้กลับเป็น พ.ค. ที่ถูกต้อง)
3. ระบบแชทให้คำแนะนำการเงิน (Conversational Financial AI): สั่งการ Gemini 2.5 Flash 
   เพื่อทำหน้าที่เป็นที่ปรึกษา คอยตอบคำถาม วิเคราะห์ปัญหา และประมวลผลคำพูดของผู้ใช้ 
   แล้วโต้ตอบกลับไปเป็นข้อความภาษาไทยที่อ่านง่ายและเป็นธรรมชาติบนหน้าจอแชท
4. ระบบบันทึกรายจ่ายด้วยเสียง (Multimodal Voice-to-Expense): รับไฟล์เสียงพูดดิบ (.wav) อัปโหลดขึ้นคลัง Gemini 
   เพื่อให้ AI ฟังเสียงภาษาไทยโดยตรง แล้วสกัดพฤติกรรมออกมาเป็น JSON Object ส่งกลับไปกรอกฟอร์มทันที
=========================================================================================
"""

import cv2
import pytesseract
from flask import Flask, request, jsonify, render_template_string
import numpy as np
import base64
import traceback
import re
import difflib
from google import genai
from google.genai import types 
import os
import json
import re
import os
from dotenv import load_dotenv

load_dotenv()
# เริ่มต้นระบบเว็บเฟรมเวิร์ก Flask
app = Flask(__name__)

# เชื่อมต่อกับบริการ Google GenAI SDK พร้อมระบุ API Key สำหรับเรียกใช้โมเดล Gemini
client = genai.Client(api_key=os.environ.get("GEMINI_API_KEY"))

# ---------------------------------------------------------
# 🛠️ CONFIGURATION (ตั้งค่าระบบภายใน)
# ---------------------------------------------------------
# ระบุที่อยู่พาธของโปรแกรม Tesseract OCR ในเครื่องคอมพิวเตอร์ เพื่อให้คลังพิมพ์ภาษาไทยทำงานได้
pytesseract.pytesseract.tesseract_cmd = r'C:\Program Files\Tesseract-OCR\tesseract.exe'

# รายชื่อเดือนย่อไทยมาตรฐาน 12 เดือน สำหรับนำไปใช้เปรียบเทียบความใกล้เคียงของข้อความ (Fuzzy Matching)
VALID_MONTHS = ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."]

def get_base64_img(img):
    """ ฟังก์ชันเสริม: แปลงรูปภาพของ OpenCV ให้กลายเป็นรหัสข้อความ Base64 (สำหรับใช้ในระบบ Debug หรือพัฒนาต่อยอด) """
    _, buffer = cv2.imencode('.jpg', img)
    return base64.b64encode(buffer).decode('utf-8')

def clean_kbank_date(ocr_text):
    """
    🌟 [ลอจิกอัจฉริยะ: ทำความสะอาดและซ่อมแซมข้อความวันที่โอนจากสลิป]
    หน้าที่: รับข้อความดิบจาก OCR มาหั่นชิ้นส่วน ปรับปรุงตัวสะกดที่เพี้ยน และแปลงเป็นฟอร์แมต "วัน เดือน ย่อ ปี"
    """
    # 1. ลบช่องว่างในข้อความออกให้หมดเพื่อให้ Regex ตรวจจับตำแหน่งตัวเลขได้ง่ายขึ้น
    text_nospace = ocr_text.replace(" ", "")
    
    # 2. ใช้ Regular Expression ค้นหาแพทเทิร์น: (ตัวเลขวัน 1-2 หลัก) (ตัวหนังสือเดือนย่อ) (ตัวเลขปีพ.ศ. 2 หลัก)
    match = re.search(r'(\d{1,2})(.*?)(\d{2})', text_nospace)
    if match:
        day = match.group(1)        # เก็บตัวเลขวัน
        raw_month = match.group(2)  # เก็บตัวหนังสือเดือนที่ยังไม่คลีน
        year = match.group(3)       # เก็บตัวเลขปี พ.ศ. สองหลัก
        
        # ดักจับเคสข้อยกเว้น: ถ้า OCR อ่านปี พ.ศ. 2569 พลาดเป็นเลขคู่อื่นๆ ให้ดึงกลับมาเป็นเลข "69"
        if year in ["09", "89", "05"]: year = "69"
        
        # 3. แก้ไขข้อผิดพลาดของฟอนต์ภาษาไทยยอดฮิตที่ Tesseract มักจะอ่านสับสนกับอักษรภาษาอังกฤษ
        raw_month = raw_month.replace('w', 'พ').replace('W', 'พ').replace('A', 'ค').replace('a', 'ค').replace('ด', 'ค')
        
        # 4. ทำ Fuzzy Matching ค้นหาเดือนที่ใกล้เคียงที่สุดจากลิสต์ VALID_MONTHS ป้องกันเคสอ่านพยัญชนะสลับ
        closest = difflib.get_close_matches(raw_month, VALID_MONTHS, n=1, cutoff=0.1)
        final_month = closest[0] if closest else "ม.ค."
        
        # 5. ลอจิกซ่อมแซมจุดบกพร่องพิเศษ: ดักจับเคสที่คำว่า "พ.ค." (พฤษภาคม) โดนอ่านเพี้ยนเป็น "พ.ย." (พฤศจิกายน)
        # โดยเช็กว่าถ้าผลลัพธ์หลุดเป็น พ.ย. แต่ในข้อความดิบมีตัว "ค" หรือ "ด" ปนอยู่ ให้ปัดกลับมาเป็น "พ.ค." ทันที
        if final_month == "พ.ย." and ("ค" in raw_month or "ด" in raw_month): 
            final_month = "พ.ค."
            
        return f"{day} {final_month} {year}"
    return ocr_text

# ---------------------------------------------------------
# 🌐 WEB UI & ROUTES (ส่วนจัดการท่อรับส่งข้อมูลบนเว็บ)
# ---------------------------------------------------------
@app.route('/', methods=['GET'])
def index():
    """ หน้าแรกของ Backend สำหรับเปิดทดสอบดูสถานะการออนไลน์บนเบราว์เซอร์ """
    return '''
    <!doctype html>
    <title>SpendSmart AI</title>
    <body style="font-family: Arial; padding: 20px; background: #121212; color: white;">
        <h2>🛠️ SpendSmart AI Backend</h2>
        <p>ระบบพร้อมทำงาน: รองรับ OCR, Voice, และ AI Chat Conversational</p>
    </body>
    '''

@app.route('/analyze-spending', methods=['POST'])
def analyze_spending():
    """
    [Endpoint: วิเคราะห์รายจ่ายรายเดือนประจำหน้า Dashboard]
    รับค่า: ยอดเงินรวม และรายการแจกแจงหมวดหมู่
    ทำหน้าที่: ส่งต่อไปให้ Gemini วิเคราะห์หาจุดบกพร่องและสรุปคำแนะนำการประหยัดเงินส่งกลับไปโชว์
    """
    try:
        data = request.json
        total = data.get('total', 0)
        categories = data.get('categories', [])

        # เขียนคำสั่งสั่งการ (Prompt) บังคับสไตล์และขอบเขตคำตอบให้ AI แสดงความเชี่ยวชาญแบบกระชับ
        prompt = f"""
        คุณคือผู้เชี่ยวชาญด้านการเงินส่วนบุคคลที่คอยให้คำแนะนำอย่างเป็นกันเองและเข้าใจง่าย
        นี่คือข้อมูลการใช้จ่ายของฉันในเดือนนี้:
        - ยอดรวมทั้งหมด: {total} บาท
        - รายละเอียดแต่ละหมวด: {categories}

        ช่วยวิเคราะห์พฤติกรรมการใช้จ่ายของฉันให้หน่อย:
        - หมวดไหนที่ฉันใช้เงินเปลืองเกินไปไหม?
        - ขอคำแนะนำสั้นๆ 2-3 ข้อเพื่อประหยัดเงินในเดือนหน้า
        ตอบเป็นภาษาไทย สั้นๆ กระชับ และเป็นธรรมชาติ
        """

        # สั่งยิงประมวลผลไปยังโมเดลหลัก gemini-2.5-flash
        response = client.models.generate_content(
            model='gemini-2.5-flash',
            contents=prompt
        )
        return jsonify({"status": "success", "advice": response.text})
    except Exception as e:
        traceback.print_exc()
        return jsonify({"status": "error", "message": str(e)}), 500

@app.route('/chat', methods=['POST'])
def chat_with_ai():
    """
    [Endpoint: ระบบพูดคุยและให้คำแนะนำผ่านแชท AI คลีนเวอร์ชัน]
    ทำหน้าที่: คุยโต้ตอบเรื่องการเงินกับผู้ใช้โดยตรงแบบ Text-based เป็นธรรมชาติ ไม่ขัดจังหวะผู้ใช้ด้วยป๊อปอัพ
    """
    try:
        data = request.json
        user_message = data.get('message', '')         # ข้อความที่ผู้ใช้พิมพ์แชทเข้ามา
        financial_context = data.get('context', '')    # ข้อมูลสถานะกระเป๋าเงินผู้ใช้ในปัจจุบันที่ส่งแนบมา

        print(f"💬 ได้รับข้อความแชท: {user_message}")

        # ปรับความเข้าใจของ AI ให้เน้นการตอบข้อความพูดคุยแนะนำการเงินตามบริบทจริง
        system_instruction = f"""
        You are SpendSmart AI, a helpful Thai financial assistant.
        Context: {financial_context}
        Provide financial tips, answer budgeting questions, and talk to the user naturally in Thai.
        """

        config = types.GenerateContentConfig(
            system_instruction=system_instruction,
            temperature=0.7 # ปรับความยืดหยุ่นให้ AI คิดคำพูดโต้ตอบได้เป็นธรรมชาติและลื่นไหล
        )

        response = client.models.generate_content(
            model='gemini-2.5-flash',
            contents=user_message,
            config=config
        )

        reply_text = response.text.strip() if response.text else "ฉันพร้อมช่วยคุณคิดวิเคราะห์เรื่องการเงินแล้วค่ะ มีอะไรอยากปรึกษาไหมคะ?"

        # 🌟 ส่งข้อความที่ AI คุยตอบกลับไปให้หน้าจอมือถือแสดงผลในรูปแบบข้อความแชทปกติอย่างปลอดภัย
        return jsonify({
            "status": "success",
            "reply": reply_text
        })

    except Exception as e:
        traceback.print_exc()
        return jsonify({"status": "error", "message": str(e)}), 500

@app.route('/debug-scan', methods=['POST'])
def debug_scan():
    """
    [Endpoint พิเศษ: สำหรับเปิดทดสอบดีบั๊กการตัดภาพสลิปบนหน้าเว็บเบราว์เซอร์]
    ทำหน้าที่เหมือนหน้าสแกนสลิปหลัก แต่จะแปลงผลลัพธ์พ่นออกมาเป็นหน้าเว็บ HTML 
    ช่วยให้นักพัฒนาส่องดูได้ทันทีว่าพิกัดพิกเซลที่เราครอบตัดภาพสลิปมีความคลาดเคลื่อนหรือไม่
    """
    try:
        file = request.files['image']
        img_array = np.frombuffer(file.read(), np.uint8)
        img = cv2.imdecode(img_array, cv2.IMREAD_COLOR)
        
        # ปรับความกว้างสูงของสลิปให้เข้าสู่ขนาดมาตรฐาน (1080 x 1920) เพื่อให้ตำแหน่งพิกัดแม่นยำทุกใบ
        img_resized = cv2.resize(img, (1080, 1920))

        # ครอบตัดตำแหน่งเจาะจงกล่องข้อมูล (Region of Interest) จากสลิปโอนเงินธนาคารกสิกรไทย
        date_crop = img_resized[132:234, 48:470]       # พิกัดกล่อง "วัน-เวลาโอน" บนแถวบนสลิป
        amount_crop = img_resized[1520:1602, 380:554] # พิกัดกล่อง "จำนวนเงินสุทธิ" ด้านล่างสลิป

        # --- ขั้นตอนย่อย: ประมวลผลและแกะยอดเงิน ---
        gray_amount = cv2.cvtColor(amount_crop, cv2.COLOR_BGR2GRAY)
        _, thresh_amount = cv2.threshold(gray_amount, 150, 255, cv2.THRESH_BINARY_INV) # ทำการทำภาพทวิภาคขาวดำกลับด้าน (Invert Binary) เพื่อลบสัญญาณรบกวนพื้นหลัง
        amount_text = pytesseract.image_to_string(thresh_amount, config='--psm 7 -c tessedit_char_whitelist=0123456789.').strip()

        # --- ขั้นตอนย่อย: ประมวลผลและแกะวันที่ภาษาไทย ---
        gray_date = cv2.cvtColor(date_crop, cv2.COLOR_BGR2GRAY)
        gray_date = cv2.resize(gray_date, None, fx=2, fy=2, interpolation=cv2.INTER_CUBIC) # ขยายขนาดภาพ 2 เท่าด้วยเทคนิค Interpolation เพื่อให้ตัวหนังสือไทยคมชัดขึ้น
        gray_date = cv2.medianBlur(gray_date, 3) # ใช้ตัวกรองสัญญาณรบกวนแบบเบลอค่ากลาง (Median Blur) ลบเม็ดฝุ่นพิกเซลขยะออก
        _, thresh_date = cv2.threshold(gray_date, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU) # ใช้ลอจิกวิเคราะห์หาค่าวิกฤตของสีอัตโนมัติด้วยวิธี Otsu's Thresholding
        
        raw_date = pytesseract.image_to_string(thresh_date, lang='tha', config='--psm 6').strip() # สั่งอ่านตัวหนังสือโหมดภาษาไทย
        date_text = clean_kbank_date(raw_date) # วิ่งเข้าลอจิกทำความสะอาดฟอนต์ไทยและจับคู่เดือนย่อ

        # ประกอบร่างรหัสโครงสร้างเว็บส่งกลับไปแสดงผลบนจอคอมพิวเตอร์
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

@app.route('/voice-to-expense', methods=['POST'])
def voice_to_expense():
    """
    [Endpoint: บันทึกบัญชีด้วยระบบเสียงพูดภาษาไทย]
    ความสามารถพิเศษ: ใช้เทคนิค Multimodal AI ข้ามขีดจำกัดระบบจำเสียงพูดแบบเดิมๆ (Speech-to-Text) 
    โดยการโยนไฟล์เสียงดิบให้ Gemini ทำหน้าที่รับฟังทำความเข้าใจบริบทภาษาพูดและแกะข้อมูลเป็น JSON โดยตรง
    """
    try:
        # 1. รับไฟล์เสียงที่บันทึกส่งมาจากไมโครโฟนมือถือฝั่งแอปพลิเคชัน C#
        audio_file = request.files['audio']
        temp_path = "temp_audio.wav"
        audio_file.save(temp_path) # เซฟเป็นไฟล์เสียงชั่วคราวในฝั่งหลังบ้าน

        # 2. ทำการอัปโหลดไฟล์เสียงชั่วคราวนี้ขึ้นสู่เซิร์ฟเวอร์ระบบจัดเก็บไฟล์ความเร็วสูงของ Google GenAI
        uploaded_file = client.files.upload(file=temp_path)

        # 3. ร่าง prompt ออกคำสั่งจัดระเบียบโครงสร้างข้อมูลข้ามโครงสร้างภาษา บังคับให้ AI คืนคำตอบกลับมาเฉพาะรูปแบบรหัส JSON เท่านั้น
        prompt = """
        ฟังเสียงนี้แล้วสกัดข้อมูลรายจ่าย/รายรับ ออกมาเป็น JSON เท่านั้น
        รูปแบบ: {"amount": ตัวเลข, "type": "Expense" หรือ "Income", "subCategory": "หมวดหมู่สั้นๆ", "note": "รายละเอียด"}
        ตอบแคี่ JSON ห้ามมีข้อความอื่นหรือ markdown
        """

        # 4. ส่งข้อมูลแบบมัลติโมดอล (รวมทั้งไฟล์เสียงวัตถุดิบและข้อความคำสั่งตัวหนังสือ) ไปให้โมเดลประมวลผล
        response = client.models.generate_content(
            model='gemini-2.5-flash',
            contents=[uploaded_file, prompt]
        )
        
        # 5. ทำความสะอาดเครื่อง: สั่งลบไฟล์เสียงชั่วคราวทั้งในเครื่องโลคอลและบนระบบคลาวด์ทิ้งทันทีเพื่อความปลอดภัยของข้อมูลผู้ใช้
        os.remove(temp_path)
        client.files.delete(name=uploaded_file.name)

        # 6. คลีนสัญลักษณ์กากบาทโค้ดส่วนเกิน (Markdown Blocks) ที่ AI มักแอบแถมพ่วงมากับรหัสข้อความข้อความ
        raw_text = response.text.strip()
        json_match = re.search(r'\{.*\}', raw_text, re.DOTALL)
        if json_match:
            raw_text = json_match.group(0)
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
    """
    [Endpoint API หลัก: ดำเนินงานสแกนรูปภาพสลิปโอนเงินของฝั่งโมบายล์แอป]
    กระบวนการ: แปลงไฟล์รูป -> ปรับขนาดพิกัด -> ทำ Preprocessing ตัดสัญญาณรบกวนด้วย OpenCV -> ยิงแกะอักษรด้วย Tesseract -> ส่ง JSON กลับคืนมือถือ
    """
    try:
        file = request.files['image']
        img_array = np.frombuffer(file.read(), np.uint8)
        img = cv2.imdecode(img_array, cv2.IMREAD_COLOR)
        
        # ล็อกมิติขนาดภาพของสลิปโอนเงินให้อยู่ในไซส์ตายตัว ป้องกันปัญหาพิกัดเคลื่อนตำแหน่งกรณีรุ่นมือถือจับภาพต่างกัน
        img_resized = cv2.resize(img, (1080, 1920))

        # --- ส่วนที่ 1: แกะและถอดรหัสรหัสกล่อง จำนวนเงิน (Amount) ---
        # เจาะจงครอบตัดพิกัดบริเวณยอดตัวเลขเงินโอนของสลิปธนาคารกสิกรไทย
        amount_crop = img_resized[1520:1602, 380:554]
        gray_amount = cv2.cvtColor(amount_crop, cv2.COLOR_BGR2GRAY)
        # ปรับพื้นภาพให้เป็นขาวดำสมบูรณ์แบบกลับด้าน เพื่อขับให้ลายเส้นพิกเซลตัวเลขโดดเด่นขึ้นมาตัดกับพื้นผิว
        _, thresh_amount = cv2.threshold(gray_amount, 150, 255, cv2.THRESH_BINARY_INV)
        # สั่งเรียกใช้ Tesseract พร้อมตั้งค่า Whitelist ให้จำกัดการอ่านแกะเฉพาะอักขระที่เป็นตัวเลข 0-9 และเครื่องหมายจุดทศนิยมเท่านั้น ช่วยลดอาการอ่านตัวอักษรเพี้ยนได้มหาศาล
        amount_text = pytesseract.image_to_string(thresh_amount, config='--psm 7 -c tessedit_char_whitelist=0123456789.').strip()

        # --- ส่วนที่ 2: แกะและถอดรหัสกล่อง วันที่โอนเงิน (Date) ---
        # เจาะจงครอบตัดพิกัดบริเวณข้อความวันที่เวลาของสลิปธนาคารกสิกรไทย
        date_crop = img_resized[132:234, 48:470]
        gray_date = cv2.cvtColor(date_crop, cv2.COLOR_BGR2GRAY)
        # ขยายความคมชัดเพื่อลบอาการภาพแตกเป็นเหลี่ยมพิกเซลแตก (Anti-aliasing Adjustment)
        gray_date = cv2.resize(gray_date, None, fx=2, fy=2, interpolation=cv2.INTER_CUBIC)
        gray_date = cv2.medianBlur(gray_date, 3)
        # ใช้สูตรของ Otsu's ทลายขีดจำกัดการคำนวณแสงเงาสะท้อนบนสลิปแบบอัตโนมัติ
        _, thresh_date = cv2.threshold(gray_date, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU)
        
        # สั่งประมวลผล OCR โหมดดึงคลังพจนานุกรมภาษาไทย ('tha')
        raw_date = pytesseract.image_to_string(thresh_date, lang='tha', config='--psm 6').strip()
        # นำข้อความวันที่ดิบที่แกะได้ วิ่งผ่านลอจิกตัวช่วยทำความสะอาดและซ่อมแซมตัวสะกดคำย่อภาษาไทย
        date_text = clean_kbank_date(raw_date)

        # ส่งคำตอบกลับไปหาโมบายล์แอปพลิเคชัน .NET MAUI ในรูปแบบมาตรฐานสากลความเร็วสูง
        return jsonify({
            "status": "success",
            "amount": amount_text if amount_text else "0",
            "date": date_text
        })
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

# จุดปล่อยตัวเซิร์ฟเวอร์เปิดใช้งานไอพีเบอร์กลางพอร์ต 5000 พร้อมเปิดโหมด Debug ยิงรายงานคำแจ้งเตือน
if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)