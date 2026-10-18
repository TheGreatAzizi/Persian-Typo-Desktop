# Persian Typo Fixer Desktop | core/fixer.py
# Python port of the browser extension fixes.js logic

import re

PTF_DEFAULTS = {
    "enabled": True,
    "fixArabicYK": True,
    "fixArabicAlef": True,
    "fixTashkeel": True,
    "fixKashida": True,
    "fixZWNJ": True,
    "fixAttachedHa": True,
    "fixCompounds": True,
    "fixCommonTypos": True,
    "fixPunctuation": True,
    "fixPersianPunct": True,
    "fixDoubleSpace": True,
    "fixArabicDigits": True,
    "fixLatinDigits": False,
    "fixSeparators": False,
    "fixRepeat": False,
    "fixQuotes": False,
    "notices": True,
    "disabledSites": [],
    "customWords": [],
}

ATTACHED_HA_EXCEPTIONS = {
    "تنها", "تنهای", "تنهایی", "رها", "رهای", "رهایی",
    "چها", "کها",
}

MI_ATTACHED_EXCEPTIONS = {
    "میز", "میزان", "میرزا", "میدان", "مینا", "مینو", "میوه",
    "میان", "میانگین", "میانی", "میهن", "میکده", "میخانه",
    "میل", "میلیون", "میلیارد", "مین", "میخ", "میخک",
    "میگو", "میمون", "میکروب", "میکروفون", "میکروسکوپ",
    "مینیمم", "میثم", "میثاق", "میعاد", "میقات",
    "میر", "میراث", "میزبان", "میزگرد", "میش", "میک",
    "مینوش", "میناگری",
}

NEMI_ATTACHED_EXCEPTIONS = {"نمین"}

FA_L = r"\u0621-\u064A\u067E\u0686\u0698\u06A9\u06AF\u06CC\u06BE\u06C0"
FA_END_STRICT = r"(?![" + FA_L + r"\u200C])"
FA_PRE = r"(^|[\s\u200C\(\[" + r"\'\"«.,،:؛!؟…?;\)\]»])"

PTF_COMPOUNDS = [
    ["کتاب خانه", "کتابخانه"],
    ["روز نامه", "روزنامه"],
    ["دانش گاه", "دانشگاه"],
    ["فرود گاه", "فرودگاه"],
    ["کار خانه", "کارخانه"],
    ["خوش حال", "خوشحال"],
    ["خوش بخت", "خوشبخت"],
    ["بد بخت", "بدبخت"],
    ["خوش مزه", "خوشمزه"],
    ["دانش جو", "دانشجو"],
    ["هم کار", "همکار"],
    ["هم فکر", "همفکر"],
    ["غیر ممکن", "غیرممکن"],
    ["غیر واقعی", "غیرواقعی"],
    ["هم چنین", "همچنین"],
    ["هم چون", "همچون"],
    ["چنان چه", "چنانچه"],
    ["آن چنان", "آنچنان"],
    ["دانش آموز", "دانش‌آموز"],
    ["پیش بینی", "پیش‌بینی"],
    ["پیش فروش", "پیش‌فروش"],
    ["پیش پرداخت", "پیش‌پرداخت"],
    ["خوش آمد", "خوش‌آمد"],
    ["نامه رسان", "نامه‌رسان"],
    ["ساعت ساز", "ساعت‌ساز"],
    ["کتاب ساز", "کتاب‌ساز"],
    ["کمک رسان", "کمک‌رسان"],
    ["خوش اخلاق", "خوش‌اخلاق"],
    ["خوش رفتار", "خوش‌رفتار"],
    ["بد اخلاق", "بداخلاق"],
    ["هم زمان", "هم‌زمان"],
    ["هم کلاسی", "هم‌کلاسی"],
    ["هم اتاقی", "هم‌اتاقی"],
    ["هم مدرسه", "هم‌مدرسه‌ای"],
    ["به طور", "به‌طور"],
    ["بنی آدم", "بنی‌آدم"],
    ["بسم الله", "بسم‌الله"],
    ["به هر حال", "به‌هرحال"],
    ["به عنوان", "به‌عنوان"],
    ["بعنوان", "به‌عنوان"],
    ["به نفع", "به‌نفع"],
    ["به ضرر", "به‌ضرر"],
    ["به همراه", "به‌همراه"],
    ["به علاوه", "به‌علاوه"],
    ["به وسیله", "به‌وسیله"],
    ["بوسیله", "به‌وسیله"],
    ["به خاطر", "به‌خاطر"],
    ["به نظر", "به‌نظر"],
    ["از جمله", "ازجمله"],
    ["از قبیل", "ازقبیل"],
    ["با وجود", "باوجود"],
    ["در نتیجه", "درنتیجه"],
    ["در واقع", "درواقع"],
    ["در عین حال", "درعین‌حال"],
    ["پدر بزرگ", "پدربزرگ"],
    ["مادر بزرگ", "مادربزرگ"],
    ["خدا حافظ", "خداحافظ"],
    ["بنابر این", "بنابراین"],
    ["خوش آمدی", "خوش‌آمدی"],
    ["خوش آمدید", "خوش‌آمدید"],
    ["خوش آمدگویی", "خوش‌آمدگویی"],
    ["فوق العاده", "فوق‌العاده"],
    ["خارق العاده", "خارق‌العاده"],
    ["حیرت انگیز", "حیرت‌انگیز"],
    ["شگفت انگیز", "شگفت‌انگیز"],
    ["دل نشین", "دلنشین"],
    ["ماه عسل", "ماه‌عسل"],
    ["هفته نامه", "هفته‌نامه"],
    ["ماه نامه", "ماهنامه"],
    ["سه شنبه", "سه‌شنبه"],
    ["چهار شنبه", "چهارشنبه"],
    ["پنج شنبه", "پنجشنبه"],
    ["یک شنبه", "یکشنبه"],
    ["دو شنبه", "دوشنبه"],
    ["آخر هفته", "آخرهفته"],
    ["پس فردا", "پس‌فردا"],
    ["نیمه شب", "نیمه‌شب"],
    ["نصف شب", "نصف‌شب"],
    ["عید دیدنی", "عیددیدنی"],
    ["سیزده بدر", "سیزده‌بدر"],
    ["چهارشنبه سوری", "چهارشنبه‌سوری"],
    ["صبح بخیر", "صبح‌بخیر"],
    ["شب بخیر", "شب‌بخیر"],
    ["خدا قوت", "خداقوت"],
    ["دست مریزاد", "دست‌مریزاد"],
    ["زنده باد", "زنده‌باد"],
    ["مرده باد", "مرده‌باد"],
    ["پاینده باد", "پاینده‌باد"],
    ["نوش جان", "نوش‌جان"],
    ["به به", "به‌به"],
    ["ای ول", "ایول"],
    ["بارک الله", "بارک‌الله"],
    ["الحمد لله", "الحمدلله"],
    ["استغفر الله", "استغفرالله"],
    ["روزه دار", "روزه‌دار"],
    ["روزه خواری", "روزه‌خواری"],
    ["شراب خواری", "شراب‌خواری"],
    ["خواب آلود", "خواب‌آلود"],
    ["خواب آلودگی", "خواب‌آلودگی"],
    ["فارغ التحصیل", "فارغ‌التحصیل"],
    ["سر فراز", "سرفراز"],
    ["سر نوشت", "سرنوشت"],
    ["سر مایه", "سرمایه"],
    ["سر گرم", "سرگرم"],
    ["سر در گم", "سردرگم"],
    ["دل خوش", "دلخوش"],
    ["دل تنگ", "دلتنگ"],
    ["دل شکسته", "دلشکسته"],
    ["دل گرم", "دلگرم"],
    ["دل سرد", "دلسرد"],
    ["دل مرده", "دلمرده"],
    ["خون گرم", "خونگرم"],
    ["خون سرد", "خونسرد"],
    ["سنگ دل", "سنگدل"],
    ["یک دل", "یکدل"],
    ["یک دست", "یکدست"],
    ["یک صدا", "یکصدا"],
    ["یک رنگ", "یکرنگ"],
    ["هم صدا", "همصدا"],
    ["هم رزم", "همرزم"],
    ["هم سنگر", "همسنگر"],
    ["هم پیمان", "هم‌پیمان"],
    ["هم عقیده", "هم‌عقیده"],
    ["غیر رسمی", "غیررسمی"],
    ["غیر قانونی", "غیرقانونی"],
    ["غیر انسانی", "غیرانسانی"],
    ["غیر منتظره", "غیرمنتظره"],
    ["پیش غذا", "پیش‌غذا"],
    ["پیش درآمد", "پیش‌درآمد"],
    ["پس لرزه", "پس‌لرزه"],
    ["پس مانده", "پس‌مانده"],
    ["میان وعده", "میان‌وعده"],
    ["میان سال", "میانسال"],
    ["بین المللی", "بین‌المللی"],
    ["بین الملل", "بین‌الملل"],
    ["چاره ساز", "چاره‌ساز"],
    ["مشکل گشا", "مشکل‌گشا"],
    ["گره گشا", "گره‌گشا"],
    ["دل سوز", "دلسوز"],
    ["دل سوخته", "دلسوخته"],
    ["کار بلد", "کاربلد"],
    ["کار کشته", "کارکشته"],
    ["کار آزموده", "کارآزموده"],
    ["راه بلد", "راهبلد"],
    ["یاد آوری", "یادآوری"],
    ["یاد داشت", "یادداشت"],
    ["یاد بود", "یادبود"],
    ["گوش زد", "گوشزد"],
    ["کم حوصله", "کم‌حوصله"],
    ["کم حرف", "کم‌حرف"],
    ["کم خواب", "کم‌خواب"],
    ["کم سواد", "کم‌سواد"],
    ["کم سن", "کم‌سن"],
    ["کم کار", "کم‌کار"],
    ["کم بها", "کم‌بها"],
    ["کم ارزش", "کم‌ارزش"],
    ["کم یاب", "کمیاب"],
    ["کم نظیر", "کم‌نظیر"],
    ["پر حرف", "پرحرف"],
    ["پر رو", "پررو"],
    ["پر بیننده", "پربیننده"],
    ["پر فروش", "پرفروش"],
    ["پر طرفدار", "پرطرفدار"],
    ["پر هزینه", "پرهزینه"],
    ["پر خطر", "پرخطر"],
    ["پر سر و صدا", "پرسروصدا"],
    ["خوش بین", "خوش‌بین"],
    ["خوش قول", "خوش‌قول"],
    ["خوش سلیقه", "خوش‌سلیقه"],
    ["خوش تیپ", "خوش‌تیپ"],
    ["خوش شانس", "خوش‌شانس"],
    ["خوش طعم", "خوش‌طعم"],
    ["خوش بو", "خوشبو"],
    ["بد بین", "بدبین"],
    ["بد قول", "بدقول"],
    ["بد سلیقه", "بدسلیقه"],
    ["بد تیپ", "بدتیپ"],
    ["بد شانس", "بدشانس"],
    ["بد حال", "بدحال"],
    ["بد دهن", "بددهن"],
    ["بد زبان", "بدزبان"],
    ["بد قدم", "بدقدم"],
    ["نا خوشایند", "ناخوشایند"],
    ["نا آشنا", "ناآشنا"],
    ["نا امید", "ناامید"],
    ["نا امن", "ناامن"],
    ["نا برابر", "نابرابر"],
    ["نا دیده", "نادیده"],
    ["نا گفته", "ناگفته"],
    ["نا کرده", "ناکرده"],
    ["نا تمام", "ناتمام"],
    ["نا شناس", "ناشناس"],
    ["نا خوانا", "ناخوانا"],
    ["نا هماهنگ", "ناهماهنگ"],
    ["نا عادلانه", "ناعادلانه"],
    ["نا ممکن", "ناممکن"],
    ["نا محدود", "نامحدود"],
    ["نا مرئی", "نامرئی"],
    ["نا مفهوم", "نامفهوم"],
    ["نا متعارف", "نامتعارف"],
    ["با ادب", "باادب"],
    ["با انصاف", "باانصاف"],
    ["با وجدان", "باوجدان"],
    ["با سلیقه", "باسلیقه"],
    ["با کلاس", "باکلاس"],
    ["بی کار", "بیکار"],
    ["بی چاره", "بیچاره"],
    ["بی نوا", "بینوا"],
    ["بی کلاس", "بی‌کلاس"],
    ["به روز", "به‌روز"],
    ["به هنگام", "بهنگام"],
    ["به موقع", "بموقع"],
    ["به تدریج", "بتدریج"],
    ["به مرور", "بمرور"],
    ["به زودی", "به‌زودی"],
    ["به زحمت", "به‌زحمت"],
    ["به سختی", "به‌سختی"],
    ["به آسانی", "به‌آسانی"],
    ["به راحتی", "به‌راحتی"],
    ["به آرامی", "به‌آرامی"],
    ["به سرعت", "به‌سرعت"],
    ["به کندی", "به‌کندی"],
    ["نا گهان", "ناگهان"],
    ["هر کس", "هرکس"],
    ["هر چیز", "هرچیز"],
    ["هر جا", "هرجا"],
    ["هر کدام", "هرکدام"],
    ["هر وقت", "هروقت"],
    ["هر روز", "هرروز"],
    ["هر شب", "هرشب"],
    ["هر سال", "هرسال"],
    ["هر ماه", "هرماه"],
    ["هر هفته", "هرهفته"],
    ["هیچ کس", "هیچ‌کس"],
    ["هیچ چیز", "هیچ‌چیز"],
    ["هیچ جا", "هیچ‌جا"],
    ["هیچ کدام", "هیچ‌کدام"],
    ["هیچ وقت", "هیچ‌وقت"],
    ["همین جا", "همین‌جا"],
    ["همین طور", "همین‌طور"],
    ["همین وقت", "همین‌وقت"],
    ["همین الان", "همین‌الان"],
    ["همین امروز", "همین‌امروز"],
    ["همان جا", "همان‌جا"],
    ["همان طور", "همان‌طور"],
    ["همان وقت", "همان‌وقت"],
    ["آن جا", "آنجا"],
    ["این جا", "اینجا"],
    ["آن وقت", "آنوقت"],
    ["آن روز", "آنروز"],
    ["این روزها", "این‌روزها"],
    ["آن روزها", "آن‌روزها"],
    ["زیر نویس", "زیرنویس"],
    ["زیر زمین", "زیرزمین"],
    ["زیر مجموعه", "زیرمجموعه"],
    ["رو نویس", "رونویس"],
    ["گل فروش", "گل‌فروش"],
    ["گل خانه", "گلخانه"],
    ["ماهی تابه", "ماهیتابه"],
    ["آشپز خانه", "آشپزخانه"],
    ["اتاق خواب", "اتاق‌خواب"],
    ["دست شویی", "دستشویی"],
    ["رخت خواب", "رختخواب"],
    ["کتاب فروشی", "کتاب‌فروشی"],
    ["کتاب خوانی", "کتاب‌خوانی"],
    ["سوء استفاده", "سوءاستفاده"],
    ["سوء تفاهم", "سوءتفاهم"],
    ["سوء نیت", "سوءنیت"],
    ["سوء سابقه", "سوءسابقه"],
    ["سوء ظن", "سوءظن"],
    ["حسن نیت", "حسن‌نیت"],
    ["عکس العمل", "عکس‌العمل"],
    ["باز خورد", "بازخورد"],
    ["باز بینی", "بازبینی"],
    ["باز خوانی", "بازخوانی"],
    ["پیش کسوت", "پیشکسوت"],
    ["سنگ تمام", "سنگ‌تمام"],
    ["جان فشانی", "جان‌فشانی"],
    ["خون بها", "خون‌بها"],
    ["شیر بها", "شیربها"],
    ["چشم روشنی", "چشم‌روشنی"],
    ["چشم زخم", "چشم‌زخم"],
    ["روزنامه نگار", "روزنامه‌نگار"],
]

PTF_COMMON_TYPOS = [
    ["انشاالله", "ان‌شاءالله"],
    ["انشااله", "ان‌شاءالله"],
    ["انشالله", "ان‌شاءالله"],
    ["انشاءالله", "ان‌شاءالله"],
    ["ان شاالله", "ان‌شاءالله"],
    ["انشا الله", "ان‌شاءالله"],
    ["ان شاء الله", "ان‌شاءالله"],
    ["ماشاالله", "ماشاءالله"],
    ["ماشااله", "ماشاءالله"],
    ["ماشالله", "ماشاءالله"],
    ["ماشاء الله", "ماشاءالله"],
    ["هوله", "حوله"],
    ["سانیه", "ثانیه"],
    ["راجب", "راجع"],
    ["آزوقه", "آذوقه"],
    ["املاء", "املا"],
    ["حیات خلوت", "حیاط خلوت"],
    ["حیات خانه", "حیاط خانه"],
    ["اطاق", "اتاق"],
    ["بلیط", "بلیت"],
    ["ایشالا", "ان‌شاءالله"],
    ["ایشالله", "ان‌شاءالله"],
    ["غورباقه", "قورباغه"],
    ["قورباقه", "قورباغه"],
    ["عملت", "املت"],
    ["غیمه", "قیمه"],
    ["قرمه", "قورمه"],
    ["ازر", "عذر"],
    ["معزرت", "معذرت"],
    ["زهن", "ذهن"],
    ["لزت", "لذت"],
    ["لذیز", "لذیذ"],
    ["لطفا", "لطفاً"],
    ["مثلا", "مثلاً"],
    ["تقریبا", "تقریباً"],
    ["معمولا", "معمولاً"],
    ["حتما", "حتماً"],
    ["اصلا", "اصلاً"],
    ["فعلا", "فعلاً"],
    ["اتفاقا", "اتفاقاً"],
    ["مخصوصا", "مخصوصاً"],
    ["خصوصا", "خصوصاً"],
    ["قاعدتا", "قاعدتاً"],
    ["نسبتا", "نسبتاً"],
    ["کاملا", "کاملاً"],
    ["دقیقا", "دقیقاً"],
    ["سریعا", "سریعاً"],
    ["فورا", "فوراً"],
    ["اخیرا", "اخیراً"],
    ["قویا", "قویاً"],
    ["مکررا", "مکرراً"],
    ["صریحا", "صریحاً"],
    ["شخصا", "شخصاً"],
    ["ضمنا", "ضمناً"],
    ["واقعا", "واقعاً"],
    ["راس", "رأس"],
    ["راسا", "رأساً"],
    ["مبدا", "مبدأ"],
    ["منشا", "منشأ"],
    ["انشاء", "انشا"],
    ["امضاء", "امضا"],
    ["اعضاء", "اعضا"],
    ["اشیاء", "اشیا"],
    ["اولیاء", "اولیا"],
    ["انبیاء", "انبیا"],
    ["اشالا", "ان‌شاءالله"],
    ["حول شدم", "هول شدم"],
    ["حول شد", "هول شد"],
    ["حول شده", "هول شده"],
    ["هول محور", "حول محور"],
    ["حل دادن", "هل دادن"],
    ["حل داد", "هل داد"],
    ["غریب الوقوع", "قریب‌الوقوع"],
    ["سفر هفت سین", "سفره هفت‌سین"],
    ["سفره هفت سین", "سفره هفت‌سین"],
    ["این که", "اینکه"],
    ["آن که", "آنکه"],
]

PTF_STEM_FIXES = [
    ["نمازگذار", "نمازگزار"],
    ["بنیانگزار", "بنیانگذار"],
    ["قانونگزار", "قانونگذار"],
    ["خدمتگذار", "خدمتگزار"],
    ["خوابگذار", "خوابگزار"],
    ["سپاسگذار", "سپاسگزار"],
    ["شکرگذار", "شکرگزار"],
]

PTF_SUB_FIXES = [
    ["تاثیر", "تأثیر"],
    ["تاسیس", "تأسیس"],
    ["تاخیر", "تأخیر"],
    ["مایوس", "مأیوس"],
    ["متاسف", "متأسف"],
    ["تاسف", "تأسف"],
    ["قرمه", "قورمه"],
    ["رویا", "رؤیا"],
    ["رویت", "رؤیت"],
    ["سوال", "سؤال"],
]

PTF_ATTACHED_FIXES = [
    ["همینطور", "همین‌طور"],
    ["همونطور", "همون‌طور"],
    ["درمورد", "در مورد"],
    ["بخاطر", "به‌خاطر"],
]

PTF_NOTICE_GOTO_SUPPRESS = ["نماز", "خدمت", "خواب", "شکر", "سپاس"]


def _escape(s):
    return re.escape(s)


def ptfFixArabicYK(t):
    return t.replace("\u064A", "\u06CC").replace("\u0649", "\u06CC").replace("\u0643", "\u06A9")


def ptfFixArabicAlef(t):
    t = t.replace("\u0623", "\u0627")
    t = t.replace("\u0625", "\u0627")
    t = t.replace("\u0624", "\u0648")
    t = t.replace("\u0629", "\u0647")
    return t


def ptfFixTashkeel(t):
    t = t.replace("اً", "\uE011")
    t = re.sub(r"[\u064B-\u065F\u0670]", "", t)
    t = t.replace("\uE011", "اً")
    return t


def ptfFixKashida(t):
    return t.replace("\u0640", "")


def ptfFixArabicDigits(t):
    m = {
        "\u0660": "\u06F0", "\u0661": "\u06F1", "\u0662": "\u06F2", "\u0663": "\u06F3", "\u0664": "\u06F4",
        "\u0665": "\u06F5", "\u0666": "\u06F6", "\u0667": "\u06F7", "\u0668": "\u06F8", "\u0669": "\u06F9",
    }
    return re.sub(r"[\u0660-\u0669]", lambda ch: m.get(ch.group(0), ch.group(0)), t)


def ptfNormalizeZWNJSpaces(t):
    t = re.sub(r"[ \t\r]*\u200C[ \t\r]*", "\u200C", t)
    t = re.sub(r"\u200C{2,}", "\u200C", t)
    return t


def ptfHitsSet(word, sset):
    for e in sset:
        if word == e or e.startswith(word):
            return True
    return False


def ptfFixZWNJ(t):
    out = t
    END = FA_END_STRICT
    out = re.sub(FA_PRE + r"(می)\s+", lambda m: m.group(1) + "می\u200C", out)
    out = re.sub(FA_PRE + r"(نمی)\s+", lambda m: m.group(1) + "نمی\u200C", out)
    out = re.sub(FA_PRE + r"(بی)\s+", lambda m: m.group(1) + "بی\u200C", out)
    out = re.sub(r"([" + FA_L + r"]+[هۀ])\s+(ام|ات|اش|مان|تان|شان|ای|ایم|اید|اند|یی|یم|یت|یش)" + END, lambda m: m.group(1) + "\u200C" + m.group(2), out)
    out = re.sub(r"([" + FA_L + r"]+[هۀ])\s+ی(\s|$|[.,،:؛!؟?;…\)\]\s])", lambda m: m.group(1) + "\u200Cی" + m.group(2), out)
    base_re = r"([\u0621-\u0627\u0628\u067E\u062A\u062B\u062C\u0686\u062D\u062E\u062F\u0630\u0631\u0632\u0698\u0633\u0634\u0635\u0636\u0637\u0638\u0639\u063A\u0641\u0642\u0643\u06A9\u0644\u0645\u0646\u0648\u0649\u064A\u06CC\u0624\u0629])\s+(ام|ات|اش|مان|تان|شان)" + END

    def repl(m):
        base, suf = m.group(1), m.group(2)
        if suf == "ام":
            return base + "م"
        if suf == "ات":
            return base + "ت"
        if suf == "اش":
            return base + "ش"
        return base + suf

    out = re.sub(base_re, repl, out)
    out = re.sub(FA_PRE + r"(به|کم|بیش)\s+(ترین|تری|تر)" + END, r"\1\2\3", out)
    out = re.sub(r"([" + FA_L + r"])\s+(هایمان|هایتان|هایشان|هایم|هایت|هایش|هایی|های|ها|ترین|تری|تر)" + END, lambda m: m.group(1) + "\u200C" + m.group(2), out)
    out = ptfFixAttachedMi(out)
    return out


def ptfFixAttachedMi(t):
    out = t
    out = re.sub(FA_PRE + r"می([" + FA_L + r"]{2,})" + FA_END_STRICT,
                 lambda m: m.group(1) + "می\u200C" + m.group(2)
                 if ("می" + m.group(2)) not in MI_ATTACHED_EXCEPTIONS else m.group(0), out)
    out = re.sub(FA_PRE + r"نمی([" + FA_L + r"]{2,})" + FA_END_STRICT,
                 lambda m: m.group(1) + "نمی\u200C" + m.group(2)
                 if ("نمی" + m.group(2)) not in NEMI_ATTACHED_EXCEPTIONS else m.group(0), out)
    return out


def ptfFixAttachedHa(t):
    AFTER = r"([\s.,،:؛!؟?;…\)\]\[\"'»\u200C]|$)"
    re1 = re.compile(r"([" + FA_L + r"]{1,30}?)ها" + AFTER)
    re2 = re.compile(r"([" + FA_L + r"]{2,30}?)های" + AFTER)
    re3 = re.compile(r"([" + FA_L + r"]{2,30}?)هایی" + AFTER)

    def repl1(m):
        base, after = m.group(1), m.group(2)
        full = base + "ها"
        if full in ATTACHED_HA_EXCEPTIONS or full in ("شما", "همه", "بها"):
            return m.group(0)
        if base.endswith("ها"):
            return m.group(0)
        if len(base) < 2 and full not in ("آنها", "اینها"):
            return m.group(0)
        return base + "\u200Cها" + after

    def repl2(m):
        base, after = m.group(1), m.group(2)
        full = base + "های"
        if full in ATTACHED_HA_EXCEPTIONS or full in ("تنهای", "رهای", "شهای"):
            return m.group(0)
        if base.endswith("های"):
            return m.group(0)
        return base + "\u200Cهای" + after

    def repl3(m):
        base, after = m.group(1), m.group(2)
        full = base + "هایی"
        if full in ("تنهایی", "رهایی"):
            return m.group(0)
        if base.endswith("هایی"):
            return m.group(0)
        return base + "\u200Cهایی" + after

    t = re1.sub(repl1, t)
    t = re2.sub(repl2, t)
    t = re3.sub(repl3, t)
    return t


def ptfFixCompounds(t):
    out = t
    FA_AFTER = r"(?=[\s\u200C.,،:؛!؟?;…\)\]\[\"'»]|$)"
    for wrong, correct in PTF_COMPOUNDS:
        pattern = re.compile(FA_PRE + _escape(wrong) + FA_AFTER)
        out = pattern.sub(lambda m: m.group(1) + correct, out)
    return out


def ptfFixCommonTypos(t):
    out = t
    FA_AFTER = r"(?=[\s\u200C.,،:؛!؟?;…\)\]\[\"'»]|$)"
    for wrong, correct in PTF_COMMON_TYPOS:
        pattern = re.compile(FA_PRE + _escape(wrong) + FA_AFTER)
        out = pattern.sub(lambda m: m.group(1) + correct, out)
    return out


def ptfFixStems(t):
    out = t
    for wrong, correct in PTF_STEM_FIXES:
        out = out.replace(wrong, correct)
    return out


def ptfFixSubs(t):
    out = t
    for wrong, correct in PTF_SUB_FIXES:
        out = out.replace(wrong, correct)
    return out


def ptfFixCustomWords(t, customs):
    if not isinstance(customs, list) or len(customs) == 0:
        return t
    out = t
    for item in customs[:200]:
        if not isinstance(item, dict):
            continue
        if item.get("on") is False:
            continue
        frm = str(item.get("from", "")).strip()
        to = str(item.get("to", "")).strip()
        if not frm or not to or len(frm) > 60 or len(to) > 60:
            continue
        frm = ptfNormCustomFrom(frm)
        if not frm or frm == to:
            continue
        try:
            FA_AFTER = r"(?=[\s\u200C.,،:؛!؟?;…\)\]\[\"'»]|$)"
            pattern = re.compile(FA_PRE + _escape(frm) + FA_AFTER)
            out = pattern.sub(lambda m: m.group(1) + to, out)
        except Exception:
            pass
    return out


def ptfFixAttachedWords(t):
    out = t
    FA_AFTER = r"(?=[\s\u200C.,،:؛!؟?;…\)\]\[\"'»]|$)"
    for wrong, correct in PTF_ATTACHED_FIXES:
        pattern = re.compile(FA_PRE + _escape(wrong) + FA_AFTER)
        out = pattern.sub(lambda m: m.group(1) + correct, out)
    return out


def ptfNormCustomFrom(s):
    return ptfFixArabicAlef(ptfFixArabicYK(str(s or "")))


def ptfFixPunctuationSpacing(t):
    out = t
    out = re.sub(r"\s+([.,،:؛!؟?;…\)\]])", r"\1", out)
    out = re.sub(r"([\(\[])\s+", lambda m: m.group(1), out)
    ALL_DIGITS = r"0-9\u06F0-\u06F9\u0660-\u0669"
    out = re.sub(r"([.,،:؛!؟?;…])([^\s" + ALL_DIGITS + r".,،:؛!؟?;…])",
                 lambda m: m.group(0) if (len(m.string) > m.start() and m.string[m.start() - 1:m.start()] and
                                          re.match(r"[A-Za-z0-9]", m.string[m.start() - 1]) and
                                          re.match(r"[A-Za-z0-9]", m.group(2))) else m.group(1) + " " + m.group(2), out)
    return out


def ptfFixPersianPunct(t):
    out = t
    out = re.sub(r"\.{3,}", "…", out)
    L = r"[" + FA_L + r"\u200C]"
    out = re.sub(r"(" + L + r")\s+([,;?])", r"\1\2", out)
    DIG = r"[0-9\u06F0-\u06F9\u0660-\u0669]"

    def comma_repl(m):
        s = m.group(0)
        if s == ",":
            before = m.string[m.start() - 1] if m.start() > 0 else ""
            after = m.string[m.start() + 1] if m.start() + 1 < len(m.string) else ""
            if re.match(DIG, before or "") and re.match(DIG, after or ""):
                return s
        return "،" * len(s)

    out = re.sub(r"(?<=" + L + r"|،),+|,+(?=" + L + r"|،)", comma_repl, out)
    out = re.sub(r"(?<=" + L + r"|؛);+|;+(?=" + L + r"|؛)", lambda m: "؛" * len(m.group(0)), out)
    out = re.sub(r"(?<=" + L + r"|؟)\?+|\?+(?=" + L + r"|؟)", lambda m: "؟" * len(m.group(0)), out)
    out = re.sub(r"(?<=[\u0600-\u06FF\u200C0-9۰-۹])%|%(?=[\u0600-\u06FF\u200C])", "٪", out)
    return out


def ptfFixDoubleSpace(t):
    out = re.sub(r"[ \t]{2,}", " ", t)
    out = re.sub(r"\n{3,}", "\n\n", out)
    return out


def ptfFixLatinDigits(t):
    fa = ["۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹"]
    return re.sub(r"[0-9]", lambda d: fa[int(d.group(0))], t)


def ptfFixSeparators(t):
    out = re.sub(r"([۰-۹]),([۰-۹])", r"\1٬\2", t)
    out = re.sub(r"([۰-۹])[.\/]([۰-۹])", r"\1٫\2", out)
    return out


def ptfFixRepeat(t):
    return re.sub(r"([\u0600-\u06FF])\1{2,}", r"\1", t)


def ptfFixQuotes(t):
    return re.sub(r'"([^"\n]*[\u0600-\u06FF][^"\n]*)"', r"«\1»", t)


def ptfFixChars(t, opts):
    out = t
    if opts.get("fixArabicYK"):
        out = ptfFixArabicYK(out)
    if opts.get("fixArabicAlef"):
        out = ptfFixArabicAlef(out)
    if opts.get("fixArabicDigits"):
        out = ptfFixArabicDigits(out)
    if opts.get("fixLatinDigits"):
        out = ptfFixLatinDigits(out)
    return out


def ptfFixWords(t, opts):
    out = t
    if opts.get("fixKashida"):
        out = ptfFixKashida(out)
    if opts.get("fixTashkeel"):
        out = ptfFixTashkeel(out)
    out = ptfNormalizeZWNJSpaces(out)
    out = ptfFixCustomWords(out, opts.get("customWords", []))
    if opts.get("fixZWNJ"):
        out = ptfFixZWNJ(out)
    if opts.get("fixCompounds"):
        out = ptfFixCompounds(out)
    if opts.get("fixZWNJ"):
        out = ptfFixZWNJ(out)
    if opts.get("fixAttachedHa"):
        out = ptfFixAttachedHa(out)
    if opts.get("fixCommonTypos"):
        out = ptfFixCommonTypos(out)
    if opts.get("fixCommonTypos"):
        out = ptfFixSubs(out)
    if opts.get("fixCompounds"):
        out = ptfFixStems(out)
    if opts.get("fixCompounds"):
        out = ptfFixAttachedWords(out)
    if opts.get("fixZWNJ"):
        out = ptfFixZWNJ(out)
    if opts.get("fixSeparators"):
        out = ptfFixSeparators(out)
    if opts.get("fixPersianPunct"):
        out = ptfFixPersianPunct(out)
    if opts.get("fixPunctuation"):
        out = ptfFixPunctuationSpacing(out)
    if opts.get("fixDoubleSpace"):
        out = ptfFixDoubleSpace(out)
    if opts.get("fixPunctuation") and opts.get("fixDoubleSpace"):
        out = re.sub(r" {2,}", " ", out)
    if opts.get("fixRepeat"):
        out = ptfFixRepeat(out)
    if opts.get("fixQuotes"):
        out = ptfFixQuotes(out)
    return out


def ptfSplitStable(before):
    m = re.match(r"^([\s\S]*[\s.,،:؛!؟?;…)\]\"'»\n])([\s\S]*)$", before)
    if m and re.match(r"^[" + FA_L + r"\u200C]*$", m.group(2)):
        return {"closed": m.group(1), "open": m.group(2)}
    if re.match(r"^[" + FA_L + r"\u200C]*$", before):
        return {"closed": "", "open": before}
    return {"closed": before, "open": ""}


def ptfFixOpenWord(open_text, opts):
    if not open_text:
        return open_text
    out = open_text
    out = ptfFixCustomWords(out, opts.get("customWords", []))
    if opts.get("fixZWNJ") and not ptfHitsSet(out, MI_ATTACHED_EXCEPTIONS) and not ptfHitsSet(out, NEMI_ATTACHED_EXCEPTIONS):
        out = ptfFixAttachedMi(out)
    if opts.get("fixTashkeel"):
        out = ptfFixTashkeel(out)
    if opts.get("fixKashida"):
        out = ptfFixKashida(out)
    if opts.get("fixAttachedHa"):
        out = ptfFixAttachedHa(out)
    if opts.get("fixCompounds"):
        out = ptfFixCompounds(out)
        out = ptfFixStems(out)
        out = ptfFixAttachedWords(out)
    if opts.get("fixCommonTypos"):
        out = ptfFixCommonTypos(out)
    if opts.get("fixRepeat"):
        out = ptfFixRepeat(out)
    return out


def ptfFixText(text, opts=None):
    if not text or not isinstance(text, str):
        return text
    if not re.search(r"[\u0600-\u06FF]", text):
        return text
    opts = opts or PTF_DEFAULTS

    placeholders = []
    bare_domain = r'(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,}(?::[0-9]{1,5})?(?:/[^\s<>"\']*)?'
    url_re = re.compile(r'https?://[^\s<>"\']+|www\.[^\s<>"\']+|[A-Za-z0-9_.+-]+@[A-Za-z0-9_-]+\.[A-Za-z0-9_.]+|' + bare_domain)

    def url_repl(m):
        placeholders.append(m.group(0))
        return "\uE000" + _idx_to_letters(len(placeholders) - 1) + "\uE001"

    out = url_re.sub(url_repl, text)
    out = ptfFixChars(out, opts)
    out = ptfFixWords(out, opts)
    out = re.sub(r"\uE000([A-Z]+)\uE001",
                 lambda m: placeholders[_letters_to_idx(m.group(1))] if _letters_to_idx(m.group(1)) < len(placeholders) else m.group(0), out)
    return out


def _idx_to_letters(i):
    s = ""
    i += 1
    while i > 0:
        m = (i - 1) % 26
        s = chr(65 + m) + s
        i = (i - 1) // 26
    return s


def _letters_to_idx(s):
    idx = 0
    for ch in s:
        idx = idx * 26 + (ord(ch) - 64)
    return idx - 1


def ptfFixTypingValue(value, cursor, opts=None):
    opts = opts or PTF_DEFAULTS
    if not value or not isinstance(value, str):
        return {"text": value, "cursor": cursor}
    mapped = ptfFixChars(value, opts)
    c = max(0, min(cursor if cursor is not None else len(mapped), len(mapped)))
    before = mapped[:c]
    after = mapped[c:]
    parts = ptfSplitStable(before)
    fixed_closed = ptfFixWords(parts["closed"], opts)
    fixed_open = ptfFixOpenWord(parts["open"], opts)
    return {"text": fixed_closed + fixed_open + after,
            "cursor": len(fixed_closed) + len(fixed_open)}
