ספר פרויקט בהנדסת תוכנה

# Driver Moodle — מערכת לניהול שיעורי נהיגה, תלמידים, מורים, תשלומים, התראות ותמיכה

שדה

ערך

חלופה

שירותי אינטרנט, תכנות אסינכרוני ומסדי נתונים

שם בית הספר

מקיף ז אשדוד

שם התלמיד

אלי רפופורט

שם המנחה

אולגה גנדלמן

תאריך הגשה

05/05/2026

מסמך זה הוא הגרסה המורחבת של ספר הפרויקט. כל אלמנט בקוד מתועד עם הפנייה מדויקת לקובץ ושורה, כולל קטע קוד אמיתי מהפרויקט. כדי לצמצם נפח הספר אינו כולל את כל הקוד אלא רק קטעים מייצגים. לקוד המלא ראו את התיקיות בפרויקט.

תוכן עניינים

מבוא ורקע

מענה על דרישות המחוון

ניתוח מערכת

ארכיטקטורה כללית + App.config

בסיס הנתונים Access

שכבת ה-Model + DataContract

שכבת השרת WCF (IService1, Service1)

שכבת ה-ViewDB וגישה ל-Access

SQL מתקדם — INSERT, UPDATE, JOIN, GROUP BY

אבטחה ובדיקות קלט (PBKDF2, Validation, IValueConverter, Sanitize, SQL Injection protection)

לקוח WPF (driver-client)

לקוח Web — ASP.NET Razor Pages

לקוח MAUI

תהליכים עסקיים מקצה לקצה

התראות ושכבת Notifications

תשלומים, תמחור והנחות + דו"חות

תיקוני באגים שבוצעו

מפות ניווט (WPF / Web / MAUI flow PNG) + ירושה + Async

בדיקות

סיכום ורפלקציה

נספחים — צילומי מסך + רשימת קבצים + ביבליוגרפיה

# 1. מבוא ורקע

Driver Moodle היא מערכת מידע לניהול תהליך לימודי נהיגה. הרעיון נולד מהקושי האמיתי: קביעת שיעורים מול מורה נהיגה מתבצעת פעמים רבות דרך הודעות חוזרות, בלי יומן מסודר, בלי מעקב תשלומים ובלי מקום אחד שבו התלמיד והמורה רואים את אותו מידע.

המערכת נותנת פתרון מלא לשלושה סוגי משתמשים: תלמיד, מורה, ומנהל. בנוסף קיים פיצול לשלוש פלטפורמות לקוח: WPF (שולחני), Web (דפדפן), ו-MAUI (אנדרואיד/iOS/חלונות).

קהל יעד: תלמידי נהיגה, מורי נהיגה, מנהלי בתי ספר לנהיגה.

מטרת על: לרכז במקום אחד את ניהול השיעורים, התשלומים, ההתראות והדיווחים.

טכנולוגיות: Visual Studio, C#, WCF (BasicHttpBinding), WPF, ASP.NET Razor Pages, .NET MAUI, XAML, Razor, MS Access (.accdb) דרך OleDb.

אתגרים מרכזיים:

סנכרון בין שלושה לקוחות שונים מול אותו שרת.

שמירה על תאימות לתאריכים בפורמטים שונים בין מסדי הנתונים והתצוגות.

ניהול הרשאות לפי תפקיד (תלמיד / מורה / מנהל) בכל אחד מהלקוחות.

אבטחת סיסמאות עם hashing מודרני וסניטציית קלט.

# 2. מענה על דרישות המחוון

פרק זה ממופה ישירות מול המסמכים *פירוט דרישות חלק 1* ו-*בדיקת פרויקט וספר פרויקט עפ"י דרישות*. כל סעיף מציין היכן בפרויקט נמצא המענה (קובץ + שורה) ומפנה לפרק המפורט בהמשך הספר.

## 2.1 דרישות חובה (סעיפים 1–8 במחוון)

#

דרישה במחוון

מענה בפרויקט

מקום בקוד / בספר

1

התוכנית מהווה ממשק מלא למערכת מידע, עם שליפה ממספר טבלאות ועדכון

Service1 חושף ~70 פעולות שירות שמטפלות ב-12 טבלאות. כל פעולה היא Select/Insert/Update/Delete מובנית

WcfServiceLibrary1/WcfServiceLibrary1/IService1.cs (כל הפעולות) — פרק 7

2

שימוש בנתונים ממסד נתונים, ובלקוחות פקדים שונים

בסיס Access דרך OleDb בשרת. בלקוחות: TextBox, ComboBox, DatePicker, DataGrid, ListView, CollectionView, RadioButton, CheckBox, ProgressBar

driver-client/driver-client/StudentUI.xaml, ViewLessons.xaml, Driver/Driver/Pages/..., driver-maui/Pages/...

3

מסד מנורמל עם מספר טבלאות + טבלאות קישור

12 טבלאות. טבלאות קישור: Lessons (Student↔Teacher↔זמן), Payments (Student↔Lesson), Notifications (Sender↔Recipient), StudentCourseProgress (Student↔Module)

פרק 5

4

2/3 לקוחות בעלי ממשק נח: חלונאי / אינטרנטי / טלפוני

שלושה לקוחות: WPF (חלונאי), ASP.NET Razor Pages (אינטרנטי), .NET MAUI (סלולרי)

driver-client/, Driver/Driver/, driver-maui/ — פרקים 11, 12, 13

5

ניהול נתונים חכם — שאילתות מורכבות, חיתוך, עדכון

INNER JOIN בין Lessons↔Student↔Teacher↔Payments + GROUP BY + SUM. UPDATE לפי תנאים. דוגמה: הכנסות מאומתות עם JOIN משולב

WcfServiceLibrary1/ViewDB/LessonsDB.cs:158-192, PaymentDB.cs:200-262 — פרק 9

6

תכנות מונחה עצמים + ירושה

כל מודלי הנתונים יורשים מ-Base. ה-DBים יורשים מ-BaseDB. דוגמאות נוספות: AllUsers : List<UserInfo>, NotificationList : List<Notification>

Model/Base.cs, ViewDB/BaseDB.cs — פרק 6 + פרק חדש "ירושה" 11.X

7

מספר רמות הרשאה — מנהל, לקוח, עובד

שלוש רמות: Student, Teacher, Admin. כל לקוח משדר את Role (תלמיד/מורה) ואת IsAdmin

LogIn.xaml.cs, Login.cshtml.cs, LoginPage.xaml.cs, AppState.cs — פרק 7 + פרק 11–13

8

UI מציג רק אפשרויות בהתאם לרמת ההרשאה

_Layout.cshtml מסתיר Admin tabs לפי Session["IsAdmin"]. TeacherUI מציג AdminBadge רק אם IsAdmin. RequireRoleAsync מנתב משתמש ב-MAUI

Driver/Driver/Pages/Shared/_Layout.cshtml:75-127, driver-client/.../TeacherUI.xaml.cs:24-42, driver-maui/Services/AppState.cs:18-34

## 2.2 הרחבות שנבחרו (סעיף 9 ו-10)

לפי ההנחיות, נבחרה אפשרות (1): שני נושאים מסעיף 9 + נושאים נוספים מסעיף 10 כתמיכה.

### 2.2.1 הרחבות מסעיף 9

נושא

הסבר

מקום בקוד

A. העברת קבצים בין שרת ללקוח (תמונות) דרך ValueConverter

ImgConventer ממיר ערך טקסטואלי paid לתמונה (check.jpg / cross.png). השרת מחזיר string, הלקוח ממיר לתמונה.

driver-client/driver-client/ImgConventer.cs, ViewLessons.xaml:63-71 — פרק 10.4

B. הצפנה מסוג חד-סיטרי PBKDF2

סיסמאות נשמרות כ-hash אחרי Rfc2898DeriveBytes עם 16-byte salt + 10000 iterations + SlowEquals (constant-time).

WcfServiceLibrary1/Model/Helpers/SecurityHelper.cs:21-106 — פרק 10.1

C. מספר משתמשים באותו פרויקט

תלמידים, מורים, מנהלים — כולם משתמשים בו זמנית במערכת אחת, רואים תצוגות שונות.

פרק 7 + פרק 11–13

### 2.2.2 הרחבות מסעיף 10

נושא

הסבר

מקום בקוד

1. מחלקות IValueConverter

ImgConventer (paid → image)

driver-client/driver-client/ImgConventer.cs — פרק 10.4

2. מחלקות ValidationRule

7 כללים: AgeRangeRule, TeacherIdRule, EmailRule, PhoneRule, isAdminRule, MinLenth, LessonPriceRule

driver-client/driver-client/ValidationRules.cs:1-160 — פרק 10.3

3. Service חיצוני

WCF service מתפקד כ-service "חיצוני" שלקוחות שונים (WPF/Web/MAUI) צורכים דרך Connected Service.

WcfServiceLibrary1/WcfServiceLibrary1/App.config, Driver/Driver/Connected Services/webTOsrv/Reference.cs, driver-maui/Connected Services/DriverService/Reference.cs — פרק 4

4. Async / Threads

MAUI כולה אסינכרונית: כל קריאת WCF דרך await ServiceHelper.CallAsync. ב-WPF: DispatcherTimer מבצע polling לעדכון התראות ואישור.

driver-maui/Services/ServiceHelper.cs:26-50, driver-client/driver-client/StudentUI.xaml.cs:36-46 — פרק חדש "Async" 11.X

6. קבצים XML

App.config של WCF הוא קובץ XML שנקרא בעלייה כדי לאתחל את ה-binding והכתובת.

WcfServiceLibrary1/WcfServiceLibrary1/App.config — פרק 4

7. הגנה מפני SQL Injection

כל שאילתה משתמשת ב-OleDbParameter (placeholders ?). אין string concatenation. בנוסף IsSafeString מסנן קלט.

WcfServiceLibrary1/ViewDB/BaseDB.cs:54-90, Model/Helpers/SecurityHelper.cs:130-153 — פרק 10.1, 10.2

8. Async + Timers

DispatcherTimer ל-polling, await/async ב-MAUI, callbacks ב-Razor.

פרק 11.2 + פרק 13

## 2.3 מענה לדרישות *פירוט דרישות חלק 1*

#

דרישה

מענה

1

App.config מובא בתוך הספר עם הסבר על השדות

פרק 4 — App.config מלא + הסבר ל-address, binding, includeExceptionDetailInFaults

2

Interface עם הערות-הסבר לפני כל פעולה

פרק 7 — IService1 מחולק ל-9 קטגוריות (USER, LESSON, PAYMENT…)

3

אירועים — כפתורים, בחירה משורה ברשימה

StudentUI עם 9 כפתורים. AllStudents מציג כרטיסיות עם כפתור "Confirm" לכל תלמיד.

4

UserInterface עם בדיקות תקינות + בדיקת קלט

ולידציות ב-SignUp (Regex אימייל/טלפון, אורך מינימלי, ת.ז. מורה אמיתית). הודעת שגיאה לכל שדה.

5

דו"חות — דפי סטטיסטיקות

TeacherPaymentReports (סך הכנסות, חודש נוכחי, חוב לפי תלמיד), TeacherConfirmPayments (שיעורים לאישור), AllStudents (תלמידים מאושרים/ממתינים)

6

ממשקי משתמש ל-2-3 סוגי משתמש

תלמיד/מורה/אדמין × WPF/Web/MAUI = 9 ממשקים נפרדים

## 2.4 מבנה הספר

שער + תוכן עניינים.

מבוא, ניתוח מערכת, Use Case, DFD, עץ תהליכים.

מבנה בסיס נתונים מלא (טבלאות, שדות, קישורים).

App.config, Interface (IService1), Service (Service1), Models, ViewDB.

ממשקים ללקוחות לפי תפקידי משתמש.

בדיקות קלט, ולידציות (ValidationRules, IValueConverter, Regex).

התראות, דוחות, צ'אט, פניות תמיכה.

נספחי קוד אמיתיים + צילומי מסך + תרשימי זרימה.

# 3. ניתוח מערכת

## 3.1 מסמך ייזום

הבעיה: קביעת שיעורי נהיגה וניהול תשלומים מתבצעים ידנית ללא מערכת מרכזית.

הפתרון: מערכת שרת-לקוח שמרכזת שיעורים, תלמידים, מורים, תשלומים, הודעות, קורסים ופניות תמיכה.

ערך: חיסכון בזמן, פחות טעויות, שקיפות בין תלמיד למורה.

## 3.2 מצב קיים מול מצב עתידי

היבט

מצב קיים

מצב עתידי

תקשורת

הודעות SMS/WhatsApp פזורות

התראות מובנות בתוך המערכת

תיעוד שיעורים

יומן ידני / זיכרון

טבלת Lessons בבסיס נתונים

תשלומים

מזומן / רישום ידני

טבלת Payments עם סטטוס + שיטת תשלום

דוחות

אין

PaymentReports, ConfirmPayments

הרשאות

אין

Role-based: Student, Teacher, Admin

## 3.3 Use Cases מרכזיים

הרשמה — משתמש מזין פרטים → ולידציה → בדיקת כפילות (CheckUserExist) → שמירה ל-Student או Teacher.

התחברות — משתמש מזין שם וסיסמה → CheckUserPassword → בדיקת תפקיד CheckUserAdmin → ניתוב.

קביעת שיעור — תלמיד בוחר תאריך + שעה → AddLessonForStudent → INSERT ל-Lessons עם TeacherID משויך.

תשלום — תלמיד בוחר שיעורים לתשלום → Pay(payment) → UPDATE Lessons + INSERT Payments.

התראה — מורה שולח הודעה → SendTeacherMessage → INSERT Notifications → תלמיד רואה ב-GetUserNotifications.

ביטול שיעור — תלמיד מבטל → CancelLesson → UPDATE Lessons SET Canceled=1 → התראה אוטומטית למורה.

כתיבת ביקורת — תלמיד מדרג → UpdateRating → ממוצע מתעדכן.

ניהול קורסים — מורה יוצר/מעדכן קורסים ומודולים, תלמיד מסמן השלמה.

פנייה לתמיכה — משתמש פותח Ticket → מנהל מטפל ומסגור.

## 3.4 DFD-0 מילולי

[משתמש]   ↓[לקוח: WPF / Web / MAUI]   ↓ (HTTP SOAP via BasicHttpBinding)[WCF Proxy: driver.Service1Client / DriverService.Service1Client / webTOsrv.Service1Client]   ↓[IService1 / Service1 (WcfServiceLibrary1)]   ↓[ViewDB: UserDB, LessonsDB, PaymentDB, NotificationDB, CourseDB, SupportTicketDB]   ↓ (OleDb)[Access UsersDataBase.accdb]

## 3.5 עץ תהליכים

ניהול משתמשים → הרשמה → התחברות → ניתוב → אישור תלמיד.

ניהול שיעורים → קביעת שיעור → ביטול → תשלום → דוח.

תקשורת → התראות → צ'אט גלובלי / פרטי → פניות תמיכה.

תמחור → מחיר ברירת מחדל למורה → מחיר מותאם לתלמיד → אחוז הנחה.

# 4. ארכיטקטורה כללית

הפרויקט בנוי בארכיטקטורת 3 שכבות:

שכבת נתונים (Data): Access database .accdb + ViewDB (OleDb).

שכבת עסק (Business / Service): WCF service WcfServiceLibrary1 עם IService1 ו-Service1.

שכבת לקוח (Presentation): WPF + Web + MAUI, כל אחד עם Connected Service משלו.

מבנה התיקיות:

Driver-moodle/├── WcfServiceLibrary1/│   ├── WcfServiceLibrary1/   ← השירות (App.config, IService1, Service1)│   ├── Model/                ← מחלקות DataContract│   ├── ViewDB/               ← UserDB, LessonsDB, PaymentDB, NotificationDB...│   ├── BusinessLogic/│   └── DatabaseMigration/    ← MigratePasswordsToHash├── driver-client/            ← WPF├── Driver/Driver/            ← Web (Razor Pages)└── driver-maui/              ← MAUI (Android/Win/iOS)

קובץ הגדרת הקצה (WCF App.config) — `WcfServiceLibrary1/WcfServiceLibrary1/App.config`:

<system.serviceModel>  <bindings>    <basicHttpBinding>      <binding name="BasicHttpBinding_IService1" />    </basicHttpBinding>  </bindings>  <client>    <endpoint address="http://192.168.1.136:8733/Design_Time_Addresses/WcfServiceLibrary1/Service1/"              binding="basicHttpBinding"              bindingConfiguration="BasicHttpBinding_IService1"              contract="driver.IService1"              name="BasicHttpBinding_IService1" />  </client>  <services>    <service name="WcfServiceLibrary1.Service1">      <host>        <baseAddresses>          <add baseAddress="http://192.168.1.136:8733/Design_Time_Addresses/WcfServiceLibrary1/Service1/" />        </baseAddresses>      </host>      <endpoint address="" binding="basicHttpBinding" contract="WcfServiceLibrary1.IService1">        <identity><dns value="localhost"/></identity>      </endpoint>      <endpoint address="mex" binding="mexHttpBinding" contract="IMetadataExchange"/>    </service>  </services>  <behaviors>    <serviceBehaviors>      <behavior>        <serviceMetadata httpGetEnabled="True" httpsGetEnabled="True"/>        <serviceDebug includeExceptionDetailInFaults="True" />      </behavior>    </serviceBehaviors>  </behaviors></system.serviceModel>

הסבר: Binding מסוג basicHttpBinding נבחר כדי לאפשר תקשורת SOAP פשוטה גם עם MAUI ועם Web ASP.NET Core (שאינם תומכים ב-WSHttpBinding מלא). includeExceptionDetailInFaults="True" מאפשר שחרור פרטי שגיאה לקליינט בזמן פיתוח.

# 5. בסיס הנתונים Access

הבסיס מאוחסן בקובץ WcfServiceLibrary1/ViewDB/UsersDataBase.accdb ונגיש דרך מחרוזת חיבור OleDb.

מחרוזת החיבור — WcfServiceLibrary1/ViewDB/BaseDB.cs:25-34:

public static OleDbConnection GetConnection(){    if (connectionString == null)    {        string ApplicationBaseFolder = AppDomain.CurrentDomain.BaseDirectory;        connectionString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" +            ApplicationBaseFolder + "\\..\\..\\..\\ViewDB\\UsersDataBase.accdb;Persist Security Info=True";    }    return new OleDbConnection(connectionString);}

## 5.1 טבלאות מרכזיות

טבלה

תפקיד

שדות מרכזיים

Student

תלמידים

id, username, password (hash), email, phone, teacherId, Confirmed, lessonPrice, DiscountPercent

Teacher

מורים

id, username, password, email, phone, Rating, Rewiew, lessonPrice, IsAdmin, PaymentMethods

Lessons

שיעורים

LessonID, StudentID, TeacherID, Date, Time, paid, Canceled

Payments

תשלומים

PaymentID, StudentID, TeacherID, Amount, PaymentDate, PaymentMethod, paid, LessonId, Status, Notes

Notifications

הודעות

id, SenderId, SenderName, SenderType, RecipientId, RecipientType, Title, Message, NotificationType, IsRead, CreatedAt, ReadAt

Calendars

זמינות מורה

TeacherID, StartTime, EndTime, AvailableDays, SpecialDays, UnavailableDays

Courses

קורסים

CourseId, CourseName, Description, IsActive

CourseModules

מודולים בקורס

ModuleId, CourseId, ModuleName, Description, OrderIndex

StudentCourseProgress

התקדמות

ProgressId, StudentId, ModuleId, IsCompleted, CompletedAt

SupportTickets

פניות

TicketId, UserId, Subject, Description, Status, Priority

TicketMessages

הודעות בפנייה

MessageId, TicketId, SenderUsername, Message, IsAdmin

Chat

צ'אט גלובלי

id, message, userid, username, IsTeacher

## 5.2 הקשרים (Foreign Keys לוגיים)

Student.teacherId → Teacher.id

Lessons.StudentID → Student.id

Lessons.TeacherID → Teacher.id

Payments.LessonId → Lessons.LessonID

Payments.StudentID → Student.id

Notifications.RecipientId → Student.id או Teacher.id (תלוי ב-RecipientType).

StudentCourseProgress.StudentId → Student.id, ModuleId → CourseModules.ModuleId.

## 5.3 יצירה דינמית של טבלת Notifications

הקוד יוצר את הטבלה אוטומטית בהפעלה ראשונה אם היא חסרה, ואף יוצר אותה מחדש אם חסרות עמודות נדרשות — WcfServiceLibrary1/ViewDB/NotificationDB.cs:37-128:

private void EnsureSchema(){    if (_schemaChecked) return;    lock (_schemaLock)    {        if (_schemaChecked) return;        try        {            using (var conn = GetConnection())            {                conn.Open();                bool tableExists = false;                var tables = conn.GetSchema("Tables", new[] { null, null, null, "TABLE" });                foreach (System.Data.DataRow row in tables.Rows)                {                    if (string.Equals(row["TABLE_NAME"]?.ToString(), "Notifications",                        StringComparison.OrdinalIgnoreCase))                    { tableExists = true; break; }                }                bool needsRecreate = false;                if (tableExists)                {                    var requiredCols = new[]                    { "id","SenderId","SenderName","SenderType","RecipientId","RecipientType",                      "Title","Message","NotificationType","IsRead","CreatedAt","ReadAt" };                    var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);                    var colSchema = conn.GetSchema("Columns", new[] { null, null, "Notifications", null });                    foreach (System.Data.DataRow row in colSchema.Rows)                        existingCols.Add(row["COLUMN_NAME"]?.ToString() ?? "");                    foreach (var c in requiredCols)                        if (!existingCols.Contains(c)) { needsRecreate = true; break; }                }                if (tableExists && needsRecreate)                {                    using (var drop = new OleDbCommand("DROP TABLE [Notifications]", conn))                        drop.ExecuteNonQuery();                    tableExists = false;                }                if (!tableExists)                {                    string ddl = @"CREATE TABLE [Notifications] (                        [id] COUNTER PRIMARY KEY,                        [SenderId] LONG, [SenderName] TEXT(50), [SenderType] TEXT(20),                        [RecipientId] LONG, [RecipientType] TEXT(20),                        [Title] TEXT(255), [Message] MEMO,                        [NotificationType] TEXT(30), [IsRead] BIT,                        [CreatedAt] DATETIME, [ReadAt] DATETIME )";                    using (var cmd = new OleDbCommand(ddl, conn))                        cmd.ExecuteNonQuery();                }            }            _schemaChecked = true;        }        catch (Exception ex)        {            System.Diagnostics.Debug.WriteLine("EnsureSchema(Notifications) error: " + ex.Message);        }    }}

הסבר: השיטה משתמשת ב-conn.GetSchema() כדי לקרוא את ה-metadata של ה-DB ולוודא שהעמודות הנכונות קיימות. אם לא — היא מוחקת את הטבלה ויוצרת אותה מחדש לפי DDL מסודר. שימוש ב-lock מבטיח שהבדיקה תרוץ פעם אחת בלבד גם תחת WCF concurrency.

# 6. שכבת ה-Model

ה-Model מורכב ממחלקות עם [DataContract] ו-[DataMember] כדי שהן ייסריאליזו בצורה תקינה דרך WCF. דוגמה — WcfServiceLibrary1/Model/UserInfo.cs:10-103:

[DataContract]public class UserInfo : Base{    [DataMember] public bool IsAdmin { get; set; }    [DataMember] public string Username { get; set; }    [DataMember] public string Password { get; set; }    [DataMember] public string Email { get; set; }    [DataMember] public string Phone { get; set; }    [DataMember] public int TeacherId { get; set; }    [DataMember] public int StudentId { get; set; }    [DataMember] public double Rating { get; set; }    [DataMember] public string RatingText { get; set; }    [DataMember] public string Rewiew { get; set; }    [DataMember] public bool Confirmed { get; set; }    [DataMember] public int LessonPrice { get; set; }    [DataMember] public string PaymentMethods { get; set; }    [DataMember] public int CustomLessonPrice { get; set; }    [DataMember] public int DiscountPercent { get; set; }    public UserInfo()    {        LessonPrice = 200;        PaymentMethods = "Cash,Credit Card,Bank Transfer";        DiscountPercent = 0;        CustomLessonPrice = 0;    }    /// <summary> Returns the effective price after discount </summary>    public int GetEffectivePrice(int basePrice)    {        if (CustomLessonPrice > 0) return CustomLessonPrice;        if (DiscountPercent > 0) return basePrice - (basePrice * DiscountPercent / 100);        return basePrice;    }}

מחלקת בסיס — WcfServiceLibrary1/Model/Base.cs:

[DataContract]public class Base{    [DataMember] public int Id { get; set; }}

מחלקת Notification (התראה) — WcfServiceLibrary1/Model/Notification.cs:

[DataContract]public class Notification : Base{    [DataMember] public int SenderId { get; set; }    [DataMember] public string SenderName { get; set; }    [DataMember] public string SenderType { get; set; }    [DataMember] public int RecipientId { get; set; }    [DataMember] public string RecipientType { get; set; }    [DataMember] public string Title { get; set; }    [DataMember] public string Message { get; set; }    [DataMember] public string NotificationType { get; set; }    [DataMember] public bool IsRead { get; set; }    [DataMember] public DateTime CreatedAt { get; set; }    [DataMember] public DateTime? ReadAt { get; set; }    public Notification()    {        IsRead = false;        CreatedAt = DateTime.Now;        NotificationType = "Message";        SenderType = "System";    }}

מחלקת Lessons — WcfServiceLibrary1/ViewDB/LessonsDB.cs:9-26:

[DataContract]public class Lessons : Base{    [DataMember] public int LessonId { get; set; }    [DataMember] public int StudentId { get; set; }    [DataMember] public int TeacherId { get; set; }    [DataMember] public bool paid { get; set; }    [DataMember] public string Date { get; set; }    [DataMember] public string Time { get; set; }    [DataMember] public int Canceled { get; set; }}

מספר אטריביוטי DataContract / DataMember בפרויקט: 207 מופעים פרוסים ב-14 קבצים תחת WcfServiceLibrary1/.

# 7. שכבת השרת WCF

## 7.1 ServiceContract (Interface)

ההצהרה — WcfServiceLibrary1/WcfServiceLibrary1/IService1.cs:12-54 (קטע מייצג):

[ServiceContract]public interface IService1{    [OperationContract] void CancelLesson(int lessonId);    [OperationContract] List<Lessons> GetAllStudentLessons(int id);    [OperationContract] List<Lessons> GetAllTeacherLessons(int tid);    [OperationContract] void AddLessonForStudent(int sid, string Date, string time);    [OperationContract] void MarkLessonPaid(int id);    [OperationContract] int GetUserID(string username, string table);    [OperationContract] UserInfo GetUserById(int id, string table);    [OperationContract] AllUsers GetAllUsers();    [OperationContract] AllUsers GetAllTeacher();    [OperationContract] bool CheckUserPassword(string username, string password);    [OperationContract] bool CheckUserAdmin(string username);    [OperationContract] bool CheckUserExist(string username);    [OperationContract] bool AddUser(string name, string password, string email, string phone,                                     bool admin, int tID, int lessonPrice = 200);    // ...}

הממשק כולל היום (לאחר כל התוספות) כ-70 פעולות שירות המכוסות 9 קטגוריות: lesson, user, admin, pricing, calendar, rating, chat, payment, support ticket, course, notification.

## 7.2 מימוש Service1 — דוגמה

WcfServiceLibrary1/WcfServiceLibrary1/Service1.cs:21-92 (AddUser):

public bool AddUser(string name, string password, string email, string phone,                    bool admin, int tID, int lessonPrice = 200){    try    {        if (!SecurityHelper.IsSafeString(name, 50))   return false;        if (!SecurityHelper.IsSafeString(email, 100)) return false;        if (string.IsNullOrEmpty(password))           return false;        if (CheckUserExist(name))                     return false;        UserInfo user = new UserInfo {            Username = name, Password = password, Email = email, Phone = phone,            IsAdmin = admin, TeacherId = tID,            LessonPrice = lessonPrice > 0 ? lessonPrice : 200        };        bool worked = admin ? userDB.AddUser(user) : userDB.AddStudent(user);        if (!admin && worked) {            int sid = userDB.GetUserID(name, "Student");            allUsers.SetStudentId(name, sid);        }        return worked;    }    catch (Exception ex) { return false; }}

Service1.SendTeacherMessage — עוטף שגיאות ב-FaultException כדי שהן תגענה ללקוח גם כשה-config של השרת לא מאפשר exception details:

public void SendTeacherMessage(int teacherId, string teacherName,                               int studentId, string title, string message){    try { new NotificationDB().SendTeacherMessage(teacherId, teacherName, studentId, title, message); }    catch (Exception ex)    {        throw new System.ServiceModel.FaultException(            "SendTeacherMessage failed: " + ex.GetBaseException().Message);    }}

# 8. שכבת ה-ViewDB וגישה ל-Access

## 8.1 BaseDB — שיטות גנריות

WcfServiceLibrary1/ViewDB/BaseDB.cs:54-90 — שאילתת Select פרמטרית, מאוד חשובה לבטיחות מפני SQL Injection:

protected virtual List<Base> Select(string sqlCommandTxt, params OleDbParameter[] parameters){    List<Base> list = new List<Base>();    try    {        connection.Open();        command.CommandText = sqlCommandTxt;        command.Parameters.Clear();        if (parameters != null && parameters.Length > 0)            command.Parameters.AddRange(parameters);        reader = command.ExecuteReader();        while (reader.Read())        {            Base entity = NewEntity();            CreateModel(entity);            list.Add(entity);        }    }    catch (Exception ex) { Debug.WriteLine("Select Error: " + ex.Message); }    finally    {        if (reader != null) reader.Close();        if (connection.State == ConnectionState.Open) connection.Close();    }    return list;}

SaveChanges — INSERT/UPDATE/DELETE פרמטריים:

protected int SaveChanges(string commandText, params OleDbParameter[] parameters){    int records = 0;    OleDbCommand cmd = new OleDbCommand();    try    {        cmd.Connection = connection;        cmd.CommandText = commandText;        cmd.Parameters.Clear();        if (parameters != null) cmd.Parameters.AddRange(parameters);        connection.Open();        records = cmd.ExecuteNonQuery();    }    catch (Exception e) { Debug.WriteLine($"SaveChanges Error: {e.Message}"); }    finally { if (connection.State == ConnectionState.Open) connection.Close(); }    return records;}

## 8.2 CreateModel — מיפוי DataReader → Object

דוגמה — WcfServiceLibrary1/ViewDB/UserDB.cs:17-62:

protected override void CreateModel(Base entity){    base.CreateModel(entity);    if (entity != null)    {        try        {            UserInfo s = (UserInfo)entity;            s.Username = reader["username"].ToString();            s.Password = reader["password"].ToString();            try { s.Rewiew = reader["Rewiew"].ToString(); } catch { }            try { s.Rating = (double)reader["Rating"]; } catch { s.Rating = 0; }            try { s.Confirmed = bool.Parse(reader["Confirmed"].ToString()); }            catch { s.Confirmed = true; }            s.Email = reader["email"].ToString();            s.Phone = reader["phone"].ToString();            try { s.TeacherId = (int)reader["TeacherId"]; } catch { s.TeacherId = 0; }            if (s.TeacherId != 0) s.StudentId = (int)reader["id"];            try { s.LessonPrice = (int)reader["lessonPrice"]; }            catch { s.LessonPrice = 200; }            s.CustomLessonPrice = s.LessonPrice;            try { s.DiscountPercent = (int)reader["DiscountPercent"]; }            catch { s.DiscountPercent = 0; }            try { s.IsAdmin = bool.Parse(reader["IsAdmin"].ToString()); }            catch { s.IsAdmin = false; }            try { s.PaymentMethods = reader["PaymentMethods"].ToString(); }            catch { s.PaymentMethods = "Cash,Credit Card,Bank Transfer"; }        }        catch (Exception ex) { Debug.WriteLine("UserDB CreateModel Error: " + ex.Message); }    }}

ה-try/catch סביב כל שדה אופציונלי מאפשר תאימות אחורה גם אם עמודה חסרה ב-Access.

# 9. SQL מתקדם — INSERT, UPDATE, JOIN, GROUP BY

## 9.1 INSERT פרמטרי לטבלת Teacher

WcfServiceLibrary1/ViewDB/UserDB.cs:127-138:

string sqlstr = @"INSERT INTO [Teacher]    ([username], [password], [email], [phone], [Rating])    VALUES (?, ?, ?, ?, ?)";var parameters = new[]{    new OleDbParameter("@username", OleDbType.VarWChar) { Value = user.Username },    new OleDbParameter("@password", OleDbType.VarWChar) { Value = hashedPassword },    new OleDbParameter("@email",    OleDbType.VarWChar) { Value = user.Email ?? "" },    new OleDbParameter("@phone",    OleDbType.VarWChar) { Value = user.Phone ?? "" },    new OleDbParameter("@rating",   OleDbType.Double)   { Value = 0.0 }};int result = SaveChanges(sqlstr, parameters);

## 9.2 INSERT לטבלת Notifications (ידני, כדי ש-OleDbException ירוץ למעלה)

WcfServiceLibrary1/ViewDB/NotificationDB.cs:140-180:

public int SendNotification(Notification notification){    string sql = @"INSERT INTO [Notifications]        ([SenderId], [SenderName], [SenderType], [RecipientId], [RecipientType],         [Title], [Message], [NotificationType], [IsRead], [CreatedAt])        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";    using (var conn = GetConnection())    using (var cmd = new OleDbCommand(sql, conn))    {        cmd.Parameters.Add(new OleDbParameter("@senderId",        OleDbType.Integer)  { Value = notification.SenderId });        cmd.Parameters.Add(new OleDbParameter("@senderName",      OleDbType.VarWChar, 50)  { Value = notification.SenderName ?? "" });        cmd.Parameters.Add(new OleDbParameter("@senderType",      OleDbType.VarWChar, 20)  { Value = notification.SenderType ?? "System" });        cmd.Parameters.Add(new OleDbParameter("@recipientId",     OleDbType.Integer)  { Value = notification.RecipientId });        cmd.Parameters.Add(new OleDbParameter("@recipientType",   OleDbType.VarWChar, 20)  { Value = notification.RecipientType ?? "" });        cmd.Parameters.Add(new OleDbParameter("@title",           OleDbType.VarWChar, 255) { Value = notification.Title ?? "" });        cmd.Parameters.Add(new OleDbParameter("@message",         OleDbType.LongVarWChar)  { Value = notification.Message ?? "" });        cmd.Parameters.Add(new OleDbParameter("@notificationType",OleDbType.VarWChar, 30)  { Value = notification.NotificationType ?? "Message" });        cmd.Parameters.Add(new OleDbParameter("@isRead",          OleDbType.Boolean)  { Value = false });        cmd.Parameters.Add(new OleDbParameter("@createdAt",       OleDbType.Date)     { Value = DateTime.Now });        try        {            conn.Open();            int affected = cmd.ExecuteNonQuery();            if (affected <= 0)                throw new InvalidOperationException("INSERT into Notifications affected 0 rows.");            return affected;        }        catch (OleDbException ex)        { throw new InvalidOperationException("Notifications INSERT failed: " + ex.Message + " | SQL: " + sql, ex); }    }}

הסבר חשוב: ה-INSERT הזה לא משתמש ב-SaveChanges מה-Base, מפני שה-Base בולע את ה-OleDbException. אנחנו זקוקים שהשגיאה האמיתית תגיע ללקוח, ולכן מבצעים את ה-INSERT ידנית עם try/catch ש-throw מחדש כ-InvalidOperationException. זה מתחבר ל-FaultException ב-Service1.

## 9.3 INSERT לטבלת Lessons

WcfServiceLibrary1/ViewDB/LessonsDB.cs:90-130 (לאחר שיפור):

public void AddLessonForStudent(int sid, string date, string time){    UserDB udb = new UserDB();    int tid = udb.GetTeacherId(sid);    if (tid <= 0)        throw new InvalidOperationException(            $"AddLessonForStudent: student id={sid} has no assigned teacher.");    string sql = "INSERT INTO [Lessons] ([StudentID],[TeacherID],[Date],[Time],[paid],[Canceled]) " +                 "VALUES (?, ?, ?, ?, ?, ?)";    using (var conn = BaseDB.GetConnection())    using (var cmd = new OleDbCommand(sql, conn))    {        cmd.Parameters.Add(new OleDbParameter("@sid",      OleDbType.Integer)  { Value = sid });        cmd.Parameters.Add(new OleDbParameter("@tid",      OleDbType.Integer)  { Value = tid });        cmd.Parameters.Add(new OleDbParameter("@date",     OleDbType.VarWChar, 50) { Value = date ?? "" });        cmd.Parameters.Add(new OleDbParameter("@time",     OleDbType.VarWChar, 10) { Value = time ?? "" });        cmd.Parameters.Add(new OleDbParameter("@paid",     OleDbType.Boolean)  { Value = false });        cmd.Parameters.Add(new OleDbParameter("@canceled", OleDbType.Integer)  { Value = 0 });        try        {            conn.Open();            int affected = cmd.ExecuteNonQuery();            if (affected <= 0)                throw new InvalidOperationException("AddLessonForStudent: INSERT affected 0 rows.");        }        catch (OleDbException ex)        {            throw new InvalidOperationException(                "AddLessonForStudent failed: " + ex.Message + " | Date=" + date + " Time=" + time, ex);        }    }}

## 9.4 UPDATE עם helper משותף

WcfServiceLibrary1/ViewDB/UserDB.cs:303-340:

public void UpdateLessonPrice(int teacherId, int price)    => ExecUpdateInt("Teacher", "lessonPrice", price, teacherId);public void SetStudentLessonPrice(int studentId, int price)    => ExecUpdateInt("Student", "lessonPrice", price, studentId);public void SetStudentDiscount(int studentId, int discountPercent)    => ExecUpdateInt("Student", "DiscountPercent", discountPercent, studentId);private void ExecUpdateInt(string table, string column, int value, int id){    string sql = $"UPDATE [{table}] SET [{column}] = ? WHERE [id] = ?";    using (var conn = BaseDB.GetConnection())    using (var cmd = new OleDbCommand(sql, conn))    {        cmd.Parameters.Add(new OleDbParameter("@v",  OleDbType.Integer) { Value = value });        cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = id });        try        {            conn.Open();            int affected = cmd.ExecuteNonQuery();            if (affected <= 0)                throw new InvalidOperationException(                    $"UPDATE [{table}] SET [{column}]=? WHERE [id]={id} affected 0 rows.");        }        catch (OleDbException ex)        {            throw new InvalidOperationException(                $"UPDATE [{table}].[{column}] failed: {ex.Message}", ex);        }    }}

## 9.5 INNER JOIN: שיעורים פעילים של תלמידים מאושרים

WcfServiceLibrary1/ViewDB/LessonsDB.cs:153-168:

public int CountActiveLessonsForConfirmedStudents(int teacherId){    string sql = @"SELECT COUNT(*)                   FROM [Lessons] AS L                   INNER JOIN [Student] AS S ON L.[StudentID] = S.[id]                   WHERE L.[TeacherID] = ?                     AND L.[Canceled] = 0                     AND S.[Confirmed] = TRUE";    object result = SelectScalar(sql, new OleDbParameter("@tid", teacherId));    return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;}

## 9.6 INNER JOIN: שיעורים לא משולמים של תלמידים מאושרים

WcfServiceLibrary1/ViewDB/LessonsDB.cs:170-192:

public List<Lessons> GetUnpaidLessonsForTeacher(int teacherId){    string sql = @"SELECT L.[LessonID], L.[StudentID], L.[TeacherID],                          L.[Date], L.[Time], L.[paid], L.[Canceled]                   FROM [Lessons] AS L                   INNER JOIN [Student] AS S ON L.[StudentID] = S.[id]                   WHERE L.[TeacherID] = ?                     AND L.[paid] = FALSE                     AND L.[Canceled] = 0                     AND S.[Confirmed] = TRUE                   ORDER BY L.[Date], L.[Time]";    return Select(sql, new OleDbParameter("@tid", teacherId)).OfType<Lessons>().ToList();}

## 9.7 INNER JOIN + SUM: הכנסות מאומתות למורה

WcfServiceLibrary1/ViewDB/PaymentDB.cs:200-215:

public decimal GetVerifiedTeacherIncome(int teacherId, DateTime fromDate, DateTime toDate){    string sql = @"SELECT SUM(P.[Amount])                   FROM [Payments] AS P                   INNER JOIN [Lessons] AS L ON P.[LessonId] = L.[LessonID]                   WHERE P.[TeacherID] = ?                     AND P.[paid] = TRUE                     AND L.[Canceled] = 0                     AND P.[PaymentDate] >= ?                     AND P.[PaymentDate] <= ?";    object result = SelectScalar(sql,        new OleDbParameter("@teacherId", teacherId),        new OleDbParameter("@fromDate",  fromDate),        new OleDbParameter("@toDate",    toDate));    return (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0m;}

## 9.8 INNER JOIN + GROUP BY: פירוט הכנסות לפי תלמיד

WcfServiceLibrary1/ViewDB/PaymentDB.cs:217-262:

public Dictionary<string, decimal> GetTeacherIncomeByStudent(int teacherId, DateTime fromDate, DateTime toDate){    var result = new Dictionary<string, decimal>();    string sql = @"SELECT S.[username], SUM(P.[Amount]) AS Total                   FROM [Payments] AS P                   INNER JOIN [Student] AS S ON P.[StudentID] = S.[id]                   WHERE P.[TeacherID] = ?                     AND P.[paid] = TRUE                     AND P.[PaymentDate] >= ?                     AND P.[PaymentDate] <= ?                   GROUP BY S.[username]                   ORDER BY SUM(P.[Amount]) DESC";    try    {        connection.Open();        command.CommandText = sql;        command.Parameters.Clear();        command.Parameters.Add(new OleDbParameter("@teacherId", teacherId));        command.Parameters.Add(new OleDbParameter("@fromDate",  fromDate));        command.Parameters.Add(new OleDbParameter("@toDate",    toDate));        reader = command.ExecuteReader();        while (reader.Read())        {            string name = reader["username"].ToString();            decimal total = Convert.ToDecimal(reader["Total"]);            result[name] = total;        }    }    finally    {        if (reader != null) reader.Close();        if (connection.State == ConnectionState.Open) connection.Close();    }    return result;}

## 9.9 UPDATE עם two-step (Lessons → Payments)

WcfServiceLibrary1/ViewDB/PaymentDB.cs:107-147:

public void Pay(Payment payment){    if (payment.NumberOfPayments > 0)        payment.ParcialAmount = payment.Amount / payment.NumberOfPayments;    else    { payment.ParcialAmount = payment.Amount; payment.NumberOfPayments = 1; }    // STEP 1: סימון השיעור כשולם    if (payment.paid && payment.LessonId > 0)    {        SaveChanges("UPDATE [Lessons] SET [Paid] = ? WHERE [LessonID] = ?",            new OleDbParameter("@paid", true),            new OleDbParameter("@lessonId", payment.LessonId));    }    // STEP 2: רישום התשלום עצמו    int nextId = GetNextPaymentId();    SaveChanges(@"INSERT INTO [Payments]        ([PaymentID],[StudentID],[TeacherID],[Amount],[PaymentDate],[PaymentMethod],         [NumberOfPayments],[paid],[ParcialAmount],[LessonId],[Status],[Notes])        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",        new OleDbParameter("@paymentId",      nextId),        new OleDbParameter("@studentId",      payment.StudentID),        new OleDbParameter("@teacherId",      payment.TeacherID),        new OleDbParameter("@amount",         payment.Amount),        new OleDbParameter("@paymentDate",    payment.PaymentDate),        new OleDbParameter("@paymentMethod",  payment.PaymentMethod ?? "Cash"),        new OleDbParameter("@numberOfPayments", payment.NumberOfPayments),        new OleDbParameter("@paid",           payment.paid),        new OleDbParameter("@parcialAmount",  payment.ParcialAmount),        new OleDbParameter("@lessonId",       payment.LessonId),        new OleDbParameter("@status",         payment.Status ?? (payment.paid ? "Paid" : "Pending")),        new OleDbParameter("@notes",          payment.Notes ?? ""));}

# 10. אבטחה ובדיקות קלט

## 10.1 SecurityHelper — PBKDF2 hashing

WcfServiceLibrary1/Model/Helpers/SecurityHelper.cs:10-106:

public static class SecurityHelper{    private const int SaltSize = 16;    private const int HashSize = 20;    private const int Iterations = 10000;    public static string HashPassword(string password)    {        if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));        byte[] salt = new byte[SaltSize];        using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(salt);        byte[] hash = HashPasswordWithSalt(password, salt);        byte[] hashBytes = new byte[SaltSize + HashSize];        Array.Copy(salt, 0, hashBytes, 0, SaltSize);        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);        return Convert.ToBase64String(hashBytes);    }    public static bool VerifyPassword(string password, string hashedPassword)    {        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword)) return false;        try        {            byte[] hashBytes = Convert.FromBase64String(hashedPassword);            byte[] salt = new byte[SaltSize];            Array.Copy(hashBytes, 0, salt, 0, SaltSize);            byte[] storedHash = new byte[HashSize];            Array.Copy(hashBytes, SaltSize, storedHash, 0, HashSize);            byte[] computedHash = HashPasswordWithSalt(password, salt);            return SlowEquals(storedHash, computedHash);        }        catch { return false; }    }    private static byte[] HashPasswordWithSalt(string password, byte[] salt)    {        using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))            return pbkdf2.GetBytes(HashSize);    }    private static bool SlowEquals(byte[] a, byte[] b)    {        uint diff = (uint)a.Length ^ (uint)b.Length;        for (int i = 0; i < a.Length && i < b.Length; i++)            diff |= (uint)(a[i] ^ b[i]);        return diff == 0;    }}

SlowEquals מונע timing attacks — הזמן להשוואה לא תלוי במספר הביטים הזהים.

## 10.2 IsSafeString — סניטציית שמות משתמש ואימייל

WcfServiceLibrary1/Model/Helpers/SecurityHelper.cs:130-153:

public static bool IsSafeString(string input, int maxLength = 100){    if (string.IsNullOrEmpty(input)) return false;    if (input.Length > maxLength)    return false;    foreach (char c in input)    {        if (!char.IsLetterOrDigit(c) &&            c != ' ' && c != '@' && c != '.' && c != '-' && c != '_')            return false;    }    return true;}

## 10.3 ValidationRules ב-WPF

driver-client/driver-client/ValidationRules.cs כולל 7 כללים:

AgeRangeRule — גיל בטווח 1–99.

TeacherIdRule — בודק שמספר מורה אמיתי קיים בשרת.

EmailRule — Regex לאימייל.

PhoneRule — Regex ^[1-9][0-9]{8}$ (10 ספרות).

isAdminRule — ערך חייב להיות Student או Teacher.

MinLenth — אורך מינימלי של 4 תווים.

LessonPriceRule — מספר חיובי, עד 10,000.

דוגמה — EmailRule:

public class EmailRule : ValidationRule{    public override ValidationResult Validate(object value, CultureInfo cultureInfo)    {        string email = (string)value;        Regex regex = new Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$");        Match match = regex.Match(email);        if (match.Success) return ValidationResult.ValidResult;        else return new ValidationResult(false, "Please enter a legal Email.");    }}

ב-XAML הם מוצמדים ל-Binding:

<TextBox.Text>    <Binding Path="Email" UpdateSourceTrigger="PropertyChanged">        <Binding.ValidationRules>            <local:EmailRule />        </Binding.ValidationRules>    </Binding></TextBox.Text>

## 10.4 IValueConverter — `ImgConventer`

driver-client/driver-client/ImgConventer.cs:

public class ImgConventer : IValueConverter{    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)    {        if (value == null) return DependencyProperty.UnsetValue;        bool paid = value.ToString() == "Yes";        return paid ? "picture/check.jpg" : "picture/cross.png";    }    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)        => throw new NotImplementedException();}

ב-XAML של ViewLessons.xaml:63-71:

<DataGridTemplateColumn Header="Paid" Width="60">    <DataGridTemplateColumn.CellTemplate>        <DataTemplate>            <Image Width="16" Height="16"                   Source="{Binding Paid, Converter={StaticResource ImgConventer}}"/>        </DataTemplate>    </DataGridTemplateColumn.CellTemplate></DataGridTemplateColumn>

## 10.5 ולידציות צד-שרת ב-MAUI

driver-maui/Pages/SignUpPage.xaml.cs:10-76:

private static readonly Regex EmailRx = new(@"^[\w\.\-]+@[\w\-]+\.[\w\-\.]+$", RegexOptions.Compiled);private static readonly Regex PhoneRx = new(@"^\+?\d{7,15}$", RegexOptions.Compiled);if (username.Length < 4 || password.Length < 4) ShowError("at least 4 characters");if (password != confirm)       ShowError("Passwords do not match");if (!EmailRx.IsMatch(email))   ShowError("legal Email");if (!PhoneRx.IsMatch(phone))   ShowError("legal phone number");

## 10.6 ולידציות צד-שרת ב-Web Razor

Driver/Driver/Pages/Student/Payments.cshtml.cs:61-67:

if (selectedLessons == null || selectedLessons.Length == 0){    Message = "Please select at least one lesson to pay for.";    IsSuccess = false;    LoadData(userId.Value);    return Page();}

# 11. לקוח WPF (driver-client)

## 11.1 ניווט בין דפים — `Frame` ו-`page.Navigate(...)`

המודל ב-WPF הוא Page בתוך Frame. כל מסך ראשי (StudentUI, TeacherUI) מכיל <Frame x:Name="page"> שעליו נטענים תתי-הדפים. דוגמה — driver-client/driver-client/StudentUI.xaml.cs:127-156:

private void ScheduleLesson_Click(object sender, RoutedEventArgs e)  => page.Navigate(new ScheduleLesson());private void WriteReview_Click(object sender, RoutedEventArgs e)     => page.Navigate(new WriteRewiew());private void ViewLessons_Click(object sender, RoutedEventArgs e)     => page.Navigate(new ViewLessons());private void Payments_Click(object sender, RoutedEventArgs e)        => page.Navigate(new StudentPayment());private void Notifications_Click(object sender, RoutedEventArgs e)   => page.Navigate(new StudentNotifications());private void Review_Click(object sender, RoutedEventArgs e)          => page.Navigate(new ChooseTeacher(false));private void Chat_Click(object sender, RoutedEventArgs e)            => page.Navigate(new Chat());private void SupportTickets_Click(object sender, RoutedEventArgs e)  => page.Navigate(new MyTickets());

## 11.2 Polling thread עם DispatcherTimer

לטעינה אוטומטית של נתונים — driver-client/driver-client/StudentUI.xaml.cs:36-46:

updateAprove = new DispatcherTimer();updateAprove.Interval = TimeSpan.FromSeconds(5);updateAprove.Tick += CheckIfApproved;updateAprove.Start();notificationTimer = new DispatcherTimer();notificationTimer.Interval = TimeSpan.FromSeconds(30);notificationTimer.Tick += UpdateNotificationBadge;notificationTimer.Start();

## 11.3 ServiceGateway — ניהול אורך-חיים של ה-WCF client

driver-client/driver-client/ClientSession.cs:7-53:

public static class ServiceGateway{    public static T Use<T>(Func<Service1Client, T> action)    {        var client = new Service1Client();        try { T result = action(client); Close(client); return result; }        catch { Abort(client); throw; }    }    public static void Use(Action<Service1Client> action)        => Use(client => { action(client); return true; });    private static void Close(Service1Client client)    {        try        {            if (client.State != CommunicationState.Faulted) client.Close();            else client.Abort();        }        catch { client.Abort(); }    }}

מנגנון זה מבטיח שכל פנייה לשרת תיסגר נכון, גם במצב של תקלה.

## 11.4 ClientSession — מי המשתמש המחובר?

driver-client/driver-client/ClientSession.cs:55-87:

public static class ClientSession{    public static int CurrentUserId => LogIn.sign?.Id ?? -1;    public static int StudentId    {        get        {            if (LogIn.sign == null) return -1;            if (!LogIn.sign.IsTeacher) return LogIn.sign.Id;            return -1;        }    }    public static int TeacherId    {        get        {            if (LogIn.sign == null) return -1;            if (LogIn.sign.IsTeacher) return LogIn.sign.Id;            return ServiceGateway.Use(client => client.GetTeacherId(LogIn.sign.Id));        }    }}

## 11.5 LogIn — תהליך כניסה

driver-client/driver-client/LogIn.xaml.cs:32-65:

private void signIn_Click(object sender, RoutedEventArgs e){    string password = pass.Password;    string user = username.Text;    driver.Service1Client srv = new driver.Service1Client();    if (srv.CheckUserPassword(user, password))    {        sign.Username = user; sign.Password = password;        if (srv.CheckUserAdmin(user))        {            sign.IsTeacher = true;            sign.Id = srv.GetUserID(user, "Teacher");            page.Navigate(new TeacherUI());        }        else        {            sign.IsTeacher = false;            sign.Id = srv.GetUserID(user, "Student");            page.Navigate(new StudentUI());        }    }    else MessageBox.Show("Wrong password or username");}

## 11.6 קביעת שיעור ב-WPF — מסך ScheduleLesson

driver-client/driver-client/ScheduleLesson.xaml.cs:206-235:

private void ConfirmLesson_Click(object sender, RoutedEventArgs e){    if (lessonDatePicker.SelectedDate == null || lessonTimeComboBox.SelectedItem == null)    { MessageBox.Show("Select date and time first."); return; }    string date = lessonDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");    string time = lessonTimeComboBox.SelectedItem.ToString();    try    {        ServiceGateway.Use(client => client.AddLessonForStudent(ClientSession.StudentId, date, time));        MessageBox.Show($"Lesson booked: {date} at {time}");        page.Navigate(new StudentUI());    }    catch (Exception ex)    {        MessageBox.Show("Booking failed: " + ex.Message, "Error",            MessageBoxButton.OK, MessageBoxImage.Error);    }}

## 11.7 DataGrid עם טמפלייטים מותאמים

driver-client/driver-client/ViewLessons.xaml:12-69 — סטיילים שמטפלים בקריאות בכל מצב (selected/hover):

<Style x:Key="DarkLessonCell" TargetType="DataGridCell">    <Setter Property="Background" Value="Transparent"/>    <Setter Property="Foreground" Value="White"/>    <Style.Triggers>        <Trigger Property="IsSelected" Value="True">            <Setter Property="Background" Value="#0E1A2B"/>            <Setter Property="Foreground" Value="#00E0FF"/>        </Trigger>    </Style.Triggers></Style>

## 11.8 פירוק תאריכים בלקוח

driver-client/driver-client/ViewLessons.xaml.cs:45-77:

DateTime lessonDateTime;string combined = $"{lesson.Date} {lesson.Time}";string[] formats = {    "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm",    "dd-MM-yyyy HH:mm", "dd-MM-yyyy H:mm",    "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm",    "MM/dd/yyyy HH:mm:ss", "M/d/yyyy h:mm:ss tt"};if (!DateTime.TryParseExact(combined, formats,        CultureInfo.InvariantCulture, DateTimeStyles.None, out lessonDateTime) &&    !DateTime.TryParse(combined, CultureInfo.InvariantCulture,        DateTimeStyles.None, out lessonDateTime) &&    !DateTime.TryParse(combined, out lessonDateTime)){    Debug.WriteLine($"ViewLessons: cannot parse '{combined}'");    continue;}

ריבוי הפורמטים נחוץ מפני שכל לקוח יכול לכתוב Date בפורמט אחר.

**הערה לגבי UniformGrid:** הפרויקט אינו משתמש כיום ב-`<UniformGrid>`. כל פריסות הרשתות בנויות ב-`<Grid ColumnDefinitions=".../" RowDefinitions="...">` בלבד. אם נדרשת רשת אחידה אפשר להחליף בקלות, ראו דוגמת המסך `StudentUI.xaml` (ColumnDefinitions עם משקל `*`/`*`/`*`).

# 12. לקוח Web — ASP.NET Razor Pages

## 12.1 מבנה דפי הניווט

ב-Driver/Driver/Pages/Shared/_Layout.cshtml:75-127 הניווט שונה לפי תפקיד:

@if (Context.Session.GetString("Role") == "Teacher"){    var isAdmin = Context.Session.GetInt32("IsAdmin") == 1;    <li><a class="nav-link" asp-page="/Teacher/TeacherHome">Dashboard</a></li>    <li><a class="nav-link" asp-page="/Teacher/AllStudents">@(isAdmin ? "All Students" : "My Students")</a></li>    <li><a class="nav-link" asp-page="/Teacher/ConfirmPayments">Confirm Payments</a></li>    <li><a class="nav-link" asp-page="/Teacher/PaymentReports">Payment Reports</a></li>    <li><a class="nav-link" asp-page="/Teacher/Notifications">Notifications</a></li>    if (isAdmin) {        <li><a class="nav-link" asp-page="/Teacher/AllTeachers">All Teachers</a></li>        <li><a class="nav-link" asp-page="/Admin/ManageUsers">Manage Users</a></li>    }}else if (Context.Session.GetString("Role") == "Student"){    <li><a class="nav-link" asp-page="/Student/StudentHome">Dashboard</a></li>    <li><a class="nav-link" asp-page="/Student/ViewLessons">My Lessons</a></li>    <li><a class="nav-link" asp-page="/Student/ScheduleLesson">Schedule Lesson</a></li>    <li><a class="nav-link" asp-page="/Student/Payments">Payments</a></li>    <li><a class="nav-link" asp-page="/Student/Courses">Courses</a></li>    <li><a class="nav-link" asp-page="/Student/Notifications">Notifications</a></li>}

## 12.2 Login עם Sessions

Driver/Driver/Pages/Login.cshtml.cs:21-65:

public IActionResult OnPost(){    if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))    { ErrorMessage = "Please fill all fields"; return Page(); }    bool exists = srv.CheckUserExist(Username);    bool valid  = srv.CheckUserPassword(Username, Password);    if (!exists || !valid) { ErrorMessage = "Invalid username or password"; return Page(); }    bool isTeacher = srv.CheckUserAdmin(Username);    int  userId    = srv.GetUserID(Username, isTeacher ? "Teacher" : "Student");    bool isAdmin   = isTeacher && srv.IsUserAdmin(Username);    HttpContext.Session.SetInt32("UserId",   userId);    HttpContext.Session.SetString("Username", Username);    HttpContext.Session.SetString("Role",    isTeacher ? "Teacher" : "Student");    HttpContext.Session.SetInt32("IsAdmin",  isAdmin ? 1 : 0);    return isTeacher        ? RedirectToPage("/Teacher/TeacherHome")        : RedirectToPage("/Student/StudentHome");}

## 12.3 Role guards בכל דף

דוגמה — Driver/Driver/Pages/Student/Payments.cshtml.cs:29-41:

public IActionResult OnGet(){    var role = HttpContext.Session.GetString("Role");    if (role != "Student") return RedirectToPage("/Login");    var userId = HttpContext.Session.GetInt32("UserId");    if (userId == null)    return RedirectToPage("/Login");    LoadData(userId.Value);    return Page();}

## 12.4 שאילתת שיעורים לתשלום

Driver/Driver/Pages/Student/Payments.cshtml.cs:114-156:

private void LoadData(int studentId){    int teacherId = srv.GetTeacherId(studentId);    var teacher = srv.GetUserById(teacherId, "Teacher");    if (teacher != null && teacher.LessonPrice > 0) LessonPrice = teacher.LessonPrice;    try    {        var studentPrice = srv.GetStudentLessonPrice(studentId);        if (studentPrice > 0) LessonPrice = studentPrice;    } catch { }    var allLessons = srv.GetAllStudentLessons(studentId);    foreach (var lesson in allLessons)    {        if (lesson.Canceled == 1) continue;        if (!lesson.paid)        {            DateTime lessonDateTime = ParseLessonDateTime(lesson.Date, lesson.Time);            UnpaidLessons.Add(new LessonViewModel {                LessonId = lesson.LessonId, Date = lesson.Date, Time = lesson.Time,                DateTime = lessonDateTime            });        }    }    UnpaidLessons = UnpaidLessons.OrderBy(l => l.DateTime).ToList();    UnpaidCount = UnpaidLessons.Count;    TotalDue = UnpaidCount * LessonPrice;    var payments = srv.SelectPaymentByStudentID(studentId);    if (payments != null)    {        PaymentHistory = payments.OrderByDescending(p => p.PaymentDate).Take(10).ToList();        TotalPaid = payments.Where(p => p.paid).Sum(p => p.Amount);    }}

ParseLessonDateTime — תומך בכל הפורמטים (זהה ל-WPF):

private static DateTime ParseLessonDateTime(string date, string time){    string[] formats = { "yyyy-MM-dd HH:mm","dd-MM-yyyy HH:mm","dd/MM/yyyy HH:mm",                          "yyyy-MM-dd H:mm","MM/dd/yyyy HH:mm:ss" };    string combined = $"{date} {time}";    if (DateTime.TryParseExact(combined, formats, CultureInfo.InvariantCulture,            DateTimeStyles.None, out var dt)) return dt;    if (DateTime.TryParse(combined, CultureInfo.InvariantCulture,            DateTimeStyles.None, out dt)) return dt;    DateTime.TryParse(combined, out dt);    return dt;}

## 12.5 קביעת שיעור ב-Web (ScheduleLesson)

Driver/Driver/Pages/Student/ScheduleLesson.cshtml.cs:38-66:

public IActionResult OnPostBook(string date, string time){    var role = HttpContext.Session.GetString("Role");    if (role != "Student") return RedirectToPage("/Login");    var userId = HttpContext.Session.GetInt32("UserId");    if (userId == null) return RedirectToPage("/Login");    if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time))    { Message = "Pick a date and time first."; IsSuccess = false; LoadTimes(userId.Value); return Page(); }    try    {        srv.AddLessonForStudent(userId.Value, date, time);        Message = $"Lesson booked: {date} at {time}.";        IsSuccess = true;    }    catch (Exception ex) { Message = $"Error booking lesson: {ex.Message}"; IsSuccess = false; }    LoadTimes(userId.Value);    return Page();}

## 12.6 Razor template עם foreach + Conditional

Driver/Driver/Pages/Teacher/Notifications.cshtml:96-138:

@foreach (var notification in Model.Notifications){    <div class="card mb-3"         style="background-color: @(notification.IsRead ? "#0E1A2B" : "#1A2636");                border-left: 4px solid @GetTypeColor(notification.NotificationType);">        <h6 style="color: @GetTypeColor(notification.NotificationType);">            @GetTypeIcon(notification.NotificationType) @notification.Title            @if (!notification.IsRead) { <span class="badge bg-warning">New</span> }        </h6>        <p>@notification.Message</p>        @if (!notification.IsRead)        {            <form method="post">                <input type="hidden" name="notificationId" value="@notification.Id" />                <button type="submit" asp-page-handler="MarkRead">✓</button>            </form>        }    </div>}

# 13. לקוח MAUI

## 13.1 ServiceHelper — חיבור ל-WCF

driver-maui/Services/ServiceHelper.cs:1-52:

public static class ServiceHelper{    private const string ServiceUrl = "http://192.168.1.136:8733/Design_Time_Addresses/WcfServiceLibrary1/Service1/";    public static Service1Client GetClient()    {        var binding = new BasicHttpBinding {            MaxReceivedMessageSize = 5_000_000,            OpenTimeout    = TimeSpan.FromSeconds(30),            SendTimeout    = TimeSpan.FromSeconds(30),            ReceiveTimeout = TimeSpan.FromSeconds(30)        };        var endpoint = new EndpointAddress(ServiceUrl);        return new Service1Client(binding, endpoint);    }    public static async Task<T> CallAsync<T>(Func<Service1Client, Task<T>> action)    {        var client = GetClient();        try { return await action(client); }        finally { try { await client.CloseAsync(); } catch { client.Abort(); } }    }}

## 13.2 AppState — שמירת מצב משתמש

driver-maui/Services/AppState.cs:1-44:

public static class AppState{    public static string Username { get; set; } = "";    public static int UserId { get; set; }    public static string Role { get; set; } = "";    public static bool IsAdmin { get; set; }    public static bool IsTeacher => Role == "Teacher";    public static bool IsStudent => Role == "Student";    public static bool IsLoggedIn => UserId > 0 && !string.IsNullOrEmpty(Role);    public static async Task<bool> RequireRoleAsync(Page page, string requiredRole)    {        if (!IsLoggedIn) { await Shell.Current.GoToAsync("//Login"); return false; }        if (Role != requiredRole)        {            await page.DisplayAlert("Access denied",                $"This page requires {requiredRole} role.", "OK");            string home = Role == "Teacher" ? "//TeacherHome" : "//StudentHome";            await Shell.Current.GoToAsync(home);            return false;        }        return true;    }}

## 13.3 Shell + Routing

driver-maui/AppShell.xaml:

<Shell Shell.FlyoutBehavior="Disabled"       Shell.TabBarIsVisible="False"       Shell.NavBarIsVisible="True">    <ShellContent Route="Login"            ContentTemplate="{DataTemplate pages:LoginPage}" />    <ShellContent Route="StudentHome"      ContentTemplate="{DataTemplate pages:StudentHomePage}" />    <ShellContent Route="TeacherHome"      ContentTemplate="{DataTemplate pages:TeacherHomePage}" />    <ShellContent Route="SignUp"           ContentTemplate="{DataTemplate pages:SignUpPage}" />    <ShellContent Route="ViewLessons"      ContentTemplate="{DataTemplate pages:ViewLessonsPage}" />    <ShellContent Route="ScheduleLesson"   ContentTemplate="{DataTemplate pages:ScheduleLessonPage}" />    <ShellContent Route="Payments"         ContentTemplate="{DataTemplate pages:PaymentsPage}" />    <ShellContent Route="WriteReview"      ContentTemplate="{DataTemplate pages:WriteReviewPage}" />    <ShellContent Route="Notifications"    ContentTemplate="{DataTemplate pages:NotificationsPage}" />    <ShellContent Route="ConfirmPayments"  ContentTemplate="{DataTemplate pages:ConfirmPaymentsPage}" />    <ShellContent Route="PaymentReports"   ContentTemplate="{DataTemplate pages:PaymentReportsPage}" />    <ShellContent Route="Chat"             ContentTemplate="{DataTemplate pages:ChatPage}" /></Shell>

Shell.TabBarIsVisible="False" חוסם את ה-tab bar האוטומטי — אחרת היו מופיעים כפתורי tab של כל הדפים גם לתפקידים לא רלוונטיים.

## 13.4 Login MAUI

driver-maui/Pages/LoginPage.xaml.cs:9-58:

private async void LoginBtn_Clicked(object sender, EventArgs e){    string username = UsernameEntry.Text?.Trim() ?? "";    string password = PasswordEntry.Text ?? "";    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))    { ErrorLabel.Text = "Please fill all fields."; ErrorLabel.IsVisible = true; return; }    try    {        bool valid = await ServiceHelper.CallAsync(srv => srv.CheckUserPasswordAsync(username, password));        if (!valid) { ErrorLabel.Text = "Invalid username or password."; ErrorLabel.IsVisible = true; return; }        bool isTeacher = await ServiceHelper.CallAsync(srv => srv.CheckUserAdminAsync(username));        string role    = isTeacher ? "Teacher" : "Student";        int    userId  = await ServiceHelper.CallAsync(srv => srv.GetUserIDAsync(username, role));        bool   isAdmin = isTeacher && await ServiceHelper.CallAsync(srv => srv.IsUserAdminAsync(username));        AppState.Username = username; AppState.UserId = userId;        AppState.Role = role; AppState.IsAdmin = isAdmin;        if (role == "Student") await Shell.Current.GoToAsync("//StudentHome");        else                   await Shell.Current.GoToAsync("//TeacherHome");    }    catch (Exception ex) { ErrorLabel.Text = $"Connection error: {ex.Message}"; ErrorLabel.IsVisible = true; }}

## 13.5 דף ScheduleLessonPage MAUI

driver-maui/Pages/ScheduleLessonPage.xaml.cs:113-138:

private async void Book_Click(object sender, EventArgs e){    ResultLabel.IsVisible = false;    string time = TimePicker1.SelectedItem?.ToString();    if (string.IsNullOrEmpty(time)) { ShowResult("Pick a time first.", Colors.Red); return; }    string date = DatePicker1.Date.ToString("yyyy-MM-dd");    try    {        await ServiceHelper.CallAsync(srv => srv.AddLessonForStudentAsync(AppState.UserId, date, time));        ShowResult($"Booked {DatePicker1.Date:dd/MM/yyyy} at {time}.", Colors.Green);        await LoadTimes();    }    catch (Exception ex) { ShowResult($"Booking failed: {ex.Message}", Colors.Red); }}

## 13.6 NotificationsPage — שליחה דו-כיוונית + Mark/Delete

driver-maui/Pages/NotificationsPage.xaml.cs:96-145 (Send_Click):

private async void Send_Click(object sender, EventArgs e){    string title   = TitleEntry.Text?.Trim()   ?? "";    string message = MessageEditor.Text?.Trim() ?? "";    if (RecipientPicker.SelectedItem is not RecipientOption recipient)    { ShowSendStatus("Pick a recipient.", Colors.Red); return; }    if (string.IsNullOrEmpty(title))   { ShowSendStatus("Title required.", Colors.Red); return; }    if (string.IsNullOrEmpty(message)) { ShowSendStatus("Message required.", Colors.Red); return; }    try    {        if (AppState.Role == "Teacher")            await ServiceHelper.CallAsync(srv => srv.SendTeacherMessageAsync(                AppState.UserId, AppState.Username, recipient.Id, title, message));        else            await ServiceHelper.CallAsync(srv => srv.SendStudentMessageAsync(                AppState.UserId, AppState.Username, recipient.Id, title, message));        TitleEntry.Text = ""; MessageEditor.Text = "";        ShowSendStatus("Sent.", Colors.Green);        await LoadNotifications();    }    catch (Exception ex) { ShowSendStatus($"Send failed: {ex.Message}", Colors.Red); }}

# 14. תהליכים עסקיים מקצה לקצה

## 14.1 תהליך הרשמה ⇒ אישור ⇒ קביעת שיעור ⇒ תשלום

תלמיד נרשם → Service1.AddUser(... admin=false ...) → userDB.AddStudent(user) → INSERT ל-Student.

מורה רואה ב-AllStudents רשימת תלמידים שלו עם Confirmed=false → לוחץ "Confirm" → Service1.TeacherConfirm(sid, tid) → UPDATE Student SET Confirmed=TRUE.

תלמיד נכנס ל-StudentUI → CheckIfApproved בודק כל 5 שניות → אם student.Confirmed == true → מציג את ה-Dashboard.

תלמיד לוחץ Schedule Lesson → ScheduleLesson.xaml.cs → AddLessonForStudent(sid, "yyyy-MM-dd", "HH:mm") → INSERT לטבלת Lessons עם paid=false, Canceled=0.

תלמיד הולך ל-StudentPayment → רואה רשימת שיעורים לא משולמים → בוחר → מלחיץ Pay.

ב-PaymentDB.Pay(payment):

UPDATE Lessons SET Paid=TRUE WHERE LessonID=?.

INSERT לטבלת Payments עם paid=true, Status="Paid".

אופציה: שליחת התראה למורה (SendPaymentNotification).

## 14.2 תהליך ביטול שיעור עם התראה אוטומטית

WcfServiceLibrary1/WcfServiceLibrary1/Service1.cs:207-242:

public void CancelLesson(int lessonId){    try    {        var lesson = lessonsDB.GetLessonById(lessonId);        if (lesson != null)        {            var student = userDB.GetUserById(lesson.StudentId, "Student");            string studentName = student?.Username ?? "Unknown";            lessonsDB.CancelLesson(lessonId);            new NotificationDB().SendLessonCancelledNotification(                lesson.StudentId, studentName,                lesson.TeacherId,                lesson.Date, lesson.Time);        }        else lessonsDB.CancelLesson(lessonId);    }    catch (Exception ex)    {        Debug.WriteLine($"CancelLesson Error: {ex.Message}");        lessonsDB.CancelLesson(lessonId);    }}

SendLessonCancelledNotification (NotificationDB.cs:209-226):

public void SendLessonCancelledNotification(int studentId, string studentName,    int teacherId, string lessonDate, string lessonTime){    var notification = new Notification {        SenderId = studentId, SenderName = studentName, SenderType = "Student",        RecipientId = teacherId, RecipientType = "Teacher",        Title = "Lesson Cancelled",        Message = $"Student {studentName} has cancelled their lesson scheduled for {lessonDate} at {lessonTime}.",        NotificationType = "LessonCancelled"    };    int affectedRows = SendNotification(notification);    if (affectedRows <= 0)        throw new InvalidOperationException("Lesson cancellation notification was not saved.");}

## 14.3 תהליך ניהול תמחור

מורה ב-TeacherSettings קובע מחיר ברירת מחדל → UpdateLessonPrice(teacherId, price) → UPDATE Teacher.lessonPrice.

מורה ב-StudentPricingManagement בוחר תלמיד → SetStudentLessonPrice (מחיר מותאם) או SetStudentDiscount (אחוז הנחה).

בעת תשלום, GetEffectiveLessonPrice(studentId) מחזיר את:

מחיר מותאם אם > 0

אחרת ברירת מחדל של המורה אחרי החלת ההנחה

קוד ה-effective price — WcfServiceLibrary1/ViewDB/UserDB.cs:580-656:

public int GetStudentLessonPrice(int studentId){    string sql = "SELECT [lessonPrice], [DiscountPercent] FROM [Student] WHERE [id] = ?";    OleDbConnection conn = null; OleDbCommand cmd = null; OleDbDataReader rdr = null;    try    {        conn = BaseDB.GetConnection(); conn.Open();        cmd = new OleDbCommand(sql, conn);        cmd.Parameters.Add(new OleDbParameter("@id", studentId));        rdr = cmd.ExecuteReader();        int customPrice = 0, discountPercent = 0, teacherId = 0;        if (rdr.Read())        {            try { customPrice     = Convert.ToInt32(rdr["lessonPrice"]); } catch { }            try { discountPercent = Convert.ToInt32(rdr["DiscountPercent"]); } catch { }        }        rdr.Close();        if (customPrice > 0) return customPrice;        sql = "SELECT [teacherId] FROM [Student] WHERE [id] = ?";        cmd = new OleDbCommand(sql, conn);        cmd.Parameters.Add(new OleDbParameter("@id", studentId));        object tidResult = cmd.ExecuteScalar();        if (tidResult != null && tidResult != DBNull.Value)            teacherId = Convert.ToInt32(tidResult);        int teacherPrice = 200;        if (teacherId > 0)        {            sql = "SELECT [lessonPrice] FROM [Teacher] WHERE [id] = ?";            cmd = new OleDbCommand(sql, conn);            cmd.Parameters.Add(new OleDbParameter("@id", teacherId));            object priceResult = cmd.ExecuteScalar();            if (priceResult != null && priceResult != DBNull.Value)                teacherPrice = Convert.ToInt32(priceResult);        }        if (discountPercent > 0 && discountPercent <= 100)            return teacherPrice - (teacherPrice * discountPercent / 100);        return teacherPrice;    }    finally    {        if (rdr != null) rdr.Close();        if (conn != null && conn.State == ConnectionState.Open) conn.Close();    }}

# 15. התראות ושכבת Notifications

## 15.1 שליחת התראה ממורה לתלמיד

WcfServiceLibrary1/ViewDB/NotificationDB.cs:231-248:

public void SendTeacherMessage(int teacherId, string teacherName,                               int studentId, string title, string message){    var notification = new Notification {        SenderId = teacherId, SenderName = teacherName, SenderType = "Teacher",        RecipientId = studentId, RecipientType = "Student",        Title = title, Message = message,        NotificationType = "Message"    };    int affectedRows = SendNotification(notification);    if (affectedRows <= 0)        throw new InvalidOperationException("Teacher message was not saved.");}

## 15.2 שאילתת רשימה לפי משתמש

WcfServiceLibrary1/ViewDB/NotificationDB.cs:122-133:

public List<Notification> GetUserNotifications(int userId, string userType){    string sql = @"SELECT * FROM [Notifications]        WHERE [RecipientId] = ? AND [RecipientType] = ?        ORDER BY [CreatedAt] DESC";    return Select(sql,        new OleDbParameter("@recipientId", userId),        new OleDbParameter("@recipientType", userType))        .OfType<Notification>().ToList();}

## 15.3 סימון כנקראה / סימון הכל / מחיקה

WcfServiceLibrary1/ViewDB/NotificationDB.cs:171-220 (לאחר תיקון, ביצוע ידני שמטפל בשגיאות):

public void MarkAsRead(int notificationId){    string sql = "UPDATE [Notifications] SET [IsRead] = ?, [ReadAt] = ? WHERE [id] = ?";    using (var conn = GetConnection())    using (var cmd = new OleDbCommand(sql, conn))    {        cmd.Parameters.Add(new OleDbParameter("@isRead", OleDbType.Boolean) { Value = true });        cmd.Parameters.Add(new OleDbParameter("@readAt", OleDbType.Date)    { Value = DateTime.Now });        cmd.Parameters.Add(new OleDbParameter("@id",     OleDbType.Integer) { Value = notificationId });        try        {            conn.Open();            int affected = cmd.ExecuteNonQuery();            if (affected <= 0)                throw new InvalidOperationException(                    $"MarkAsRead: notification id={notificationId} not found.");        }        catch (OleDbException ex)        { throw new InvalidOperationException("MarkAsRead failed: " + ex.Message, ex); }    }}

# 16. תשלומים, תמחור והנחות

## 16.1 התלמיד משלם — Web Razor

Driver/Driver/Pages/Student/Payments.cshtml.cs:47-112:

public IActionResult OnPostPay(int[] selectedLessons, string paymentMethod){    var role = HttpContext.Session.GetString("Role");    if (role != "Student") return RedirectToPage("/Login");    var userId = HttpContext.Session.GetInt32("UserId");    if (userId == null) return RedirectToPage("/Login");    if (selectedLessons == null || selectedLessons.Length == 0)    { Message = "Please select at least one lesson"; IsSuccess = false; LoadData(userId.Value); return Page(); }    int teacherId = srv.GetTeacherId(userId.Value);    int price = LessonPrice;    try { var sp = srv.GetStudentLessonPrice(userId.Value); if (sp > 0) price = sp; } catch {}    foreach (var lessonId in selectedLessons)    {        var payment = new Payment {            StudentID = userId.Value, TeacherID = teacherId,            Amount = price, PaymentDate = DateTime.Now, PaymentMethod = paymentMethod,            NumberOfPayments = 1, ParcialAmount = price,            paid = true, LessonId = lessonId, Status = "Paid"        };        srv.Pay(payment);    }    Message = $"Successfully paid for {selectedLessons.Length} lesson(s)!";    IsSuccess = true;    LoadData(userId.Value);    return Page();}

## 16.2 דוח תשלומים למורה

Driver/Driver/Pages/Teacher/PaymentReports.cshtml.cs:54-149:

private void LoadData(int teacherId){    int defaultPrice = 200;    var teacher = srv.GetUserById(teacherId, "Teacher");    if (teacher != null && teacher.LessonPrice > 0) defaultPrice = teacher.LessonPrice;    var payments = srv.SelectPaymentByTeacherID(teacherId);    if (payments != null)    {        TotalEarnings = payments.Where(p => p.paid).Sum(p => p.Amount);        ThisMonthEarnings = payments            .Where(p => p.paid && p.PaymentDate.Month == DateTime.Now.Month                              && p.PaymentDate.Year  == DateTime.Now.Year)            .Sum(p => p.Amount);        // הוספת רשימה של 15 התשלומים האחרונים + שמות תלמידים        ...    }    var lessons = srv.GetAllTeacherLessons(teacherId);    var unpaidByStudent = new Dictionary<int, int>();    foreach (var lesson in lessons)    {        if (lesson.Canceled == 1) continue;        if (!lesson.paid) {            if (!unpaidByStudent.ContainsKey(lesson.StudentId))                unpaidByStudent[lesson.StudentId] = 0;            unpaidByStudent[lesson.StudentId]++;        }    }    int totalUnpaidLessons = unpaidByStudent.Values.Sum();    PendingAmount = totalUnpaidLessons * defaultPrice;    StudentsWithDebt = unpaidByStudent.Count;    // ...}

# 16.3 דו"חות (Reports)

לפי דרישה במחוון יש להציג דו"חות סטטיסטיים. הפרויקט מספק 5 דפי דו"חות במורה ובמנהל:

## 16.3.1 דו"ח הכנסות מורה (`TeacherPaymentReports`)

מציג:

TotalEarnings — סך כל ההכנסות מאי-פעם.

ThisMonthEarnings — הכנסות החודש הנוכחי בלבד (filter על PaymentDate.Month/Year).

PendingAmount — סך החובות הצפוי (שיעורים לא משולמים × מחיר).

StudentsWithDebt — מספר תלמידים עם חוב פתוח.

RecentPayments — 15 תשלומים אחרונים, ממוין לפי תאריך יורד.

DebtStudents — רשימת תלמידים חייבים, ממוין לפי סכום החוב יורד.

הקוד — Driver/Driver/Pages/Teacher/PaymentReports.cshtml.cs:54-149.

## 16.3.2 דו"ח שיעורים לאישור (`TeacherConfirmPayments`)

מציג רשימת שיעורים לא משולמים שצריכים אישור מהמורה. כולל JOIN לוגי לשם תלמיד + מחיר אישי.

## 16.3.3 דו"ח רשימת תלמידים (`AllStudents`)

מציג לכל מורה את התלמידים שלו (אדמין רואה את כולם). מציין סטטוס Confirmed.

// Driver/Driver/Pages/Teacher/AllStudents.cshtml.cs:69-89private void LoadData(int userId, string username){    IsAdmin = CheckIfAdmin(username);    if (IsAdmin)    {        var allUsers = srv.GetAllUsers();        Students = allUsers != null ? allUsers.ToList() : new List<UserInfo>();    }    else    {        var myStudents = srv.GetTeacherStudents(userId);        Students = myStudents != null ? myStudents.ToList() : new List<UserInfo>();    }    foreach (var s in Students)    {        try { EffectivePrices[s.Id] = srv.GetEffectiveLessonPrice(s.Id); }        catch { EffectivePrices[s.Id] = 200; }    }}

## 16.3.4 דו"ח שיעורי תלמיד (`ViewLessons`)

מחלק שיעורים ל-Upcoming/Completed לפי lessonDateTime >= now. מציג סטטיסטיקה: סך שיעורים, שולמו, לא שולמו.

## 16.3.5 דו"ח התראות (Notifications)

מציג ספירה לפי סוג התראה:

Cancelled lessons count

Payment notifications count

Total / Unread

// Driver/Driver/Pages/Teacher/Notifications.cshtml.cs:118-127CancelledLessonsCount    = Notifications.Count(n => n.NotificationType == "LessonCancelled");PaymentNotificationsCount= Notifications.Count(n => n.NotificationType == "PaymentReceived");

## 16.3.6 דו"חות SQL מתקדמים (server-side)

פעולות שירות שזמינות לדו"חות עתידיים (פרק 9):

GetVerifiedTeacherIncome(teacherId, fromDate, toDate) — INNER JOIN Payments+Lessons (פוסל שיעורים מבוטלים).

GetTeacherIncomeByStudent(teacherId, fromDate, toDate) — JOIN + GROUP BY username.

CountActiveLessonsForConfirmedStudents(teacherId) — INNER JOIN Lessons+Student עם תנאים.

# 17. תיקוני באגים שבוצעו

תקציר התיקונים שהוטמעו לאחרונה (מסומנים בקוד עם הערות):

#

בעיה

סיבה

פתרון

קבצים

1

"Server was unable to process the request" בשליחת התראה

טבלת Notifications חסרה / עמודות שגויות + SaveChanges בלע OleDbException

EnsureSchema() יוצר טבלה אם חסרה / חסרות עמודות. SendNotification עובד עם OleDbCommand ישיר ומעלה שגיאה אמיתית. Service1 עוטף ב-FaultException.

NotificationDB.cs, Service1.cs, App.config

2

חזרה מ-View Reviews מובילה לדף ההרשמה

ChooseTeacher.BackButton_Click תמיד ניווט ל-RoleSelection

תנאי לפי chooseMode: false → StudentUI, true → RoleSelection

ChooseTeacher.xaml.cs

3

שיעור שתלמיד קבע לא מופיע אצל המורה

תאריך נשמר כ-yyyy-MM-dd אך הפענוח ניסה רק dd-MM-yyyy

הוספת מערך פורמטים + TryParse אינווריאנטי. תיקון גם של "Today" Counter ב-TeacherUI.

Teacher_Schedule.xaml.cs, TeacherUI.xaml.cs

4

זיהוי אדמין hardcoded לשמות "admin"/"Admin"/"ADMIN"

TeacherHome ו-_Layout השוו מחרוזות

Login שומר ב-Session את IsAdmin. TeacherHome.CheckIfAdmin קורא משם, _Layout מציג כפתורים בהתאם.

Login.cshtml.cs, TeacherHome.cshtml.cs, _Layout.cshtml

5

טקסט לבן על רקע לבן ב-My Lessons

DataGrid ברירת מחדל של WPF משנה רקע ל-לבן בבחירה

סטיילים DarkLessonCell, DarkLessonRow, DarkLessonHeader.

ViewLessons.xaml

6

"Mark as read" לא עבד

OleDbException נבלע ב-SaveChanges

MarkAsRead, MarkAllAsRead, DeleteNotification עברו ל-OleDbCommand ישיר עם OleDbType מפורש. Service1 עוטף ב-FaultException.

NotificationDB.cs, Service1.cs

7

תלמיד הזמין שיעור אך לא רואה אותו

AddLessonForStudent בלע שגיאה / פורמט תאריך

INSERT ידני. ScheduleLesson עוטף try/catch. ViewLessons תומך ב-8 פורמטי תאריך.

LessonsDB.cs, ScheduleLesson.xaml.cs, ViewLessons.xaml.cs

8

Settings תמיד מציג מחיר 200

LogIn.sign.LessonPrice נשאר ברירת מחדל 200

TeacherSettings.LoadSettings שולף מהשרת. הנחה לא הופיעה בכלל כי UserDB.CreateModel לא קרא DiscountPercent. תוקן.

TeacherSettings.xaml.cs, UserDB.cs, StudentPricingManagement.xaml.cs

9

במאוי, אותו ממשק לתלמיד ולמורה

כל ה-ShellContent יצרו tab bar אוטומטי

Shell.TabBarIsVisible="False" בשורש Shell. הוספת RequireRoleAsync ב-StudentHomePage.

AppShell.xaml, StudentHomePage.xaml.cs

10

הנחת תלמיד לא נשמרה / לא הופיעה

SetStudentDiscount בלע OleDbException

פונקציית עזר ExecUpdateInt עם OleDbType מפורש + throw.

UserDB.cs

קטעי קוד מתאימים מופיעים בפרק 9 (SQL) ובפרק 15 (Notifications).

# 18. מפות ניווט

## 18.1 תרשים זרימה — WPF

תרשים זרימה ויזואלי של כל המסכים והכפתורים בלקוח WPF, מסודר לפי אזורים: Auth (כחול), Student (ירוק), Teacher (כתום), Admin (אדום), Shared (אפור).

WPF Flow

## 18.2 תרשים זרימה — Web (Razor Pages)

המסכים בלקוח האינטרנטי מסודרים לפי תפקיד. הקווים המקווקווים מציינים RedirectToPage במקרה של חוסר הרשאה (role guard).

Web Flow

## 18.3 תרשים זרימה — MAUI

תרשים MAUI Shell עם הצמתים //Login, //StudentHome, //TeacherHome ועוד. הקווים המקווקווים מציינים RequireRoleAsync שמנתב משתמשים בעלי הרשאה לא מתאימה.

MAUI Flow

## 18.4 קבצי מפת ניווט אינטראקטיביים

nav-map/WPF_NavMap.html — מפה ויזואלית של כל 28 הדפים ב-WPF, עם תמונות מסך אמיתיות וחיצי SVG בין הצמתים, מחולק לאזורים: Auth, Student, Teacher, Admin, Shared.

nav-map/Web_NavMap.html — תרשים זרימה של ה-Razor Pages (Index, Login, Logout, /Student/, /Teacher/, /Admin/*).

nav-map/MAUI_NavMap.html — תרשים זרימה של ה-MAUI Shell עם RequireRoleAsync כקווים מקווקווים.

nav-map/WPF_NavMap.md — אותו תוכן כ-Markdown לקריאה ב-VS Code / GitHub.

מבנה הניווט המרכזי ב-WPF:

MainWindow → RoleSelection → {LogIn / SignUp / ChooseTeacher / Rewiews}LogIn → StudentUI / TeacherUIStudentUI ─┬─ ScheduleLesson           ├─ ViewLessons           ├─ WriteRewiew           ├─ StudentPayment           ├─ StudentNotifications           ├─ ChooseTeacher (mode=false, view reviews only)           ├─ Chat           └─ MyTicketsTeacherUI ─┬─ AdminDashboard (IsAdmin) ─┬─ AdminTicketManagement → AdminTicketDetails           │                            └─ AdminUserManagement           ├─ AllStudents           ├─ CalendarTeacher           ├─ Teacher_Schedule           ├─ TeacherPaymentReports           ├─ TeacherConfirmPayments           ├─ TeacherNotifications           ├─ Chat           ├─ MyTickets           └─ TeacherSettings → StudentPricingManagement

# 18.5 ירושה (Inheritance)

הפרויקט עושה שימוש מובנה בירושה לפי הנחיות סעיף 6 + הרחבת "ירושה ממחלקות שנכתבו ע"י התלמיד":

## 18.5.1 ירושת Model

כל מחלקת Model יורשת ממחלקת בסיס Base:

// WcfServiceLibrary1/Model/Base.cs[DataContract]public class Base{    [DataMember] public int Id { get; set; }}// WcfServiceLibrary1/Model/UserInfo.cs:11public class UserInfo : Base { ... }// WcfServiceLibrary1/ViewDB/LessonsDB.cs:10public class Lessons : Base { ... }// WcfServiceLibrary1/ViewDB/PaymentDB.cs:10public class Payment : Base { ... }// WcfServiceLibrary1/Model/Notification.cs:27public class Notification : Base { ... }

## 18.5.2 ירושת ViewDB

כל מחלקת DB יורשת מ-BaseDB ומחויבת לממש NewEntity() ו-CreateModel():

// WcfServiceLibrary1/ViewDB/BaseDB.cspublic abstract class BaseDB{    protected abstract Base NewEntity();    protected virtual void CreateModel(Base entity) { ... }    protected virtual List<Base> Select(string sql, params OleDbParameter[] p) { ... }    protected int SaveChanges(string sql, params OleDbParameter[] p) { ... }}// WcfServiceLibrary1/ViewDB/UserDB.cs:10public class UserDB : BaseDB{    protected override Base NewEntity() => new UserInfo();    protected override void CreateModel(Base entity) { /* map reader → UserInfo */ }}// WcfServiceLibrary1/ViewDB/LessonsDB.cs:28public class LessonsDB : BaseDB { ... }// WcfServiceLibrary1/ViewDB/PaymentDB.cs:38public class PaymentDB : BaseDB { ... }// WcfServiceLibrary1/ViewDB/NotificationDB.cs:27public class NotificationDB : BaseDB { ... }

## 18.5.3 ירושת Collection

AllUsers : List<UserInfo> — אוסף משתמשים שעובר על-גבי WCF.

NotificationList : List<Notification> — אוסף התראות.

// WcfServiceLibrary1/Model/AllUsers.cs:10[CollectionDataContract]public class AllUsers : List<UserInfo>{    public AllUsers() { }    public AllUsers(IEnumerable<Base> list) : base(list.Cast<UserInfo>().ToList()) { }    public UserInfo AddUser(string name, string password, ...) { ... }    public UserInfo GetUser(string name) => this.FirstOrDefault(x => x.Username == name);}

## 18.5.4 ירושה ב-WPF Pages

כל דף ב-WPF יורש מ-System.Windows.Controls.Page. דוגמת המורשת המוארכת ביותר:

AdminTicketDetails : Page — מקבלת int ticketId בקונסטרקטור, יורשת את כל מנגנון ה-Navigation, מוסיפה לוגיקה ספציפית של פניה.

# 18.6 קוד אסינכרוני (Async)

## 18.6.1 MAUI — async/await על כל קריאת WCF

driver-maui/Services/ServiceHelper.cs:26-50:

public static async Task<T> CallAsync<T>(Func<Service1Client, Task<T>> action){    var client = GetClient();    try { return await action(client); }    finally { try { await client.CloseAsync(); } catch { client.Abort(); } }}public static async Task CallAsync(Func<Service1Client, Task> action){    var client = GetClient();    try { await action(client); }    finally { try { await client.CloseAsync(); } catch { client.Abort(); } }}

דוגמת שימוש — driver-maui/Pages/LoginPage.xaml.cs:23-46:

bool valid     = await ServiceHelper.CallAsync(srv => srv.CheckUserPasswordAsync(username, password));bool isTeacher = await ServiceHelper.CallAsync(srv => srv.CheckUserAdminAsync(username));int  userId    = await ServiceHelper.CallAsync(srv => srv.GetUserIDAsync(username, role));bool isAdmin   = isTeacher && await ServiceHelper.CallAsync(srv => srv.IsUserAdminAsync(username));

## 18.6.2 WPF — DispatcherTimer ל-polling

driver-client/driver-client/StudentUI.xaml.cs:36-46:

updateAprove = new DispatcherTimer();updateAprove.Interval = TimeSpan.FromSeconds(5);updateAprove.Tick += CheckIfApproved;updateAprove.Start();notificationTimer = new DispatcherTimer();notificationTimer.Interval = TimeSpan.FromSeconds(30);notificationTimer.Tick += UpdateNotificationBadge;notificationTimer.Start();

ה-DispatcherTimer רץ על ה-UI thread, מאפשר לעדכן controls בלי Invoke, ובכל זאת לא חוסם את הממשק.

## 18.6.3 Razor Pages — קריאות סינכרוניות

ה-Web client קורא ל-WCF סינכרונית כי הבקשה כבר רצה על thread נפרד של ASP.NET. אם נדרש אסינכרוני אפשר להחליף ל-*Async (קיים ב-Reference.cs).

# 19. בדיקות

## 19.1 בדיקת PBKDF2

WcfServiceLibrary1/DatabaseMigration/MigratePasswordsToHash.cs:140-160:

string password = "TestPassword123";string hashedPassword = SecurityHelper.HashPassword(password);bool isValid    = SecurityHelper.VerifyPassword(password, hashedPassword);bool isInvalid  = SecurityHelper.VerifyPassword("WrongPassword", hashedPassword);Console.WriteLine($"Correct -> {isValid}, Wrong -> {isInvalid}");

## 19.2 תרחישי בדיקה ידניים שבוצעו

#

תרחיש

תוצאה צפויה

1

התחברות תלמיד תקין

מסך StudentUI

2

התחברות מורה אדמין

מסך TeacherUI עם תג ADMIN

3

התחברות עם סיסמה שגויה

הודעת שגיאה, ללא ניווט

4

קביעת שיעור בעבר

DatePicker חוסם, קביעה לא נשמרת

5

קביעת שיעור פעמיים באותו זמן

המערכת מסירה את השעה מהרשימה

6

שליחת התראה ארוכה (Memo)

נשמרת מלא בטבלה (Memo column)

7

מורה לוחץ Mark Paid

UPDATE Lessons + INSERT Payments

8

תלמיד עם הנחה 20% מנסה לשלם

Effective price = baseprice * 0.8

9

מנהל ממנה תפקיד אדמין למורה

UPDATE Teacher SET IsAdmin=true

10

תלמיד פותח Ticket

INSERT SupportTickets, מנהל רואה במסך

## 19.3 בדיקות עומס SQL

ה-INNER JOINים מצטיינים ב:

מסך ConfirmPayments טוען רק שיעורים לא-משולמים של תלמידים מאושרים → GetUnpaidLessonsForTeacher (LessonsDB.cs:175-192).

דוח Income מקצה לקצה דורש INNER JOIN בין Payments, Lessons ו-Student → GetVerifiedTeacherIncome (PaymentDB.cs:200-215).

# 20. סיכום ורפלקציה

הפרויקט הצליח להעמיד תשתית עם:

ארכיטקטורה תלת-שכבתית בשירות WCF + שלושה לקוחות (WPF, Web, MAUI).

בסיס נתונים Access עם 12 טבלאות, INSERT/UPDATE/JOIN/GROUP BY בטוחים פרמטרית.

אבטחה מודרנית: PBKDF2 hashing, sanitization (IsSafeString), parameterized queries, FaultException עם הסבר ברור ללקוח.

חוויית משתמש שונה לכל תפקיד (Role-based UI) בכל לקוח.

מערכת התראות, צ'אט, פניות תמיכה ודוחות תשלומים.

אבחון קל לתחזוקה: כל שגיאת SQL מגיעה ללקוח עם הודעת מפורטת בזכות ה-Direct Command + FaultException.

מה היה אתגר: אכיפת תאימות בין פורמטי תאריך שונים (yyyy-MM-dd vs dd-MM-yyyy vs MM/dd/yyyy) בין שלושת הלקוחות. הפתרון: רשימה של פורמטים + פולבק ל-InvariantCulture.

מה למדתי: שאם שכבת DB בולעת שגיאות, באגים הופכים בלתי-נראים. צריך לאפשר ל-OleDbException להגיע ללקוח (גם אם רק בסביבת פיתוח) כדי לראות את הבעיה האמיתית.

הרחבות עתידיות:

העברת DB ל-SQL Server / SQLite.

הוספת מסכי Admin ל-MAUI (כיום קיימים רק ב-WPF/Web).

הוספת אישור דו-שלבי בהתחברות (2FA).

הצפנה ב-transit (HTTPS) — כיום WCF רץ ב-HTTP פנימי.

Push notifications אמיתיות ב-MAUI.

# 21. נספחים

## 21.1 רשימת קבצים מרכזיים

קובץ

תפקיד

WcfServiceLibrary1/WcfServiceLibrary1/IService1.cs

Service Contract — 70 פעולות

WcfServiceLibrary1/WcfServiceLibrary1/Service1.cs

מימוש Service

WcfServiceLibrary1/WcfServiceLibrary1/App.config

תצורת WCF

WcfServiceLibrary1/Model/UserInfo.cs

DataContract משתמש

WcfServiceLibrary1/Model/Notification.cs

DataContract התראה

WcfServiceLibrary1/Model/Helpers/SecurityHelper.cs

PBKDF2 + sanitize

WcfServiceLibrary1/ViewDB/BaseDB.cs

OleDb base + parameterized SELECT/INSERT

WcfServiceLibrary1/ViewDB/UserDB.cs

Add/Update users + pricing

WcfServiceLibrary1/ViewDB/LessonsDB.cs

INSERT lesson + INNER JOIN queries

WcfServiceLibrary1/ViewDB/PaymentDB.cs

Pay + verified income

WcfServiceLibrary1/ViewDB/NotificationDB.cs

Schema auto-create + send/mark/delete

driver-client/driver-client/LogIn.xaml.cs

login client side

driver-client/driver-client/StudentUI.xaml.cs

dashboard student

driver-client/driver-client/TeacherUI.xaml.cs

dashboard teacher

driver-client/driver-client/ScheduleLesson.xaml.cs

scheduling logic

driver-client/driver-client/ValidationRules.cs

7 ValidationRule classes

driver-client/driver-client/ImgConventer.cs

IValueConverter

Driver/Driver/Pages/Login.cshtml.cs

web login + sessions

Driver/Driver/Pages/Shared/_Layout.cshtml

role-aware nav

Driver/Driver/Pages/Student/ScheduleLesson.cshtml.cs

web booking

Driver/Driver/Pages/Student/Payments.cshtml.cs

web payments + parsing

driver-maui/Services/ServiceHelper.cs

MAUI WCF wrapper

driver-maui/Services/AppState.cs

role state

driver-maui/AppShell.xaml

shell routes

driver-maui/Pages/LoginPage.xaml.cs

maui login

driver-maui/Pages/NotificationsPage.xaml.cs

maui notifications

## 21.2 ביבליוגרפיה

Microsoft Docs — System.Data.OleDb Namespace.

Microsoft Docs — Windows Communication Foundation, Service Configuration.

Microsoft Docs — ASP.NET Core Razor Pages.

Microsoft Docs — .NET MAUI Shell.

OWASP — Password Storage Cheat Sheet (PBKDF2).

מסמך הנחיות לכתיבת תיק פרויקט (חלופה: שירותי אינטרנט, תכנות אסינכרוני ומסדי נתונים).

## 21.3 צילומי מסך מהמערכת (WPF)

כל הצילומים מתיקיית `nav-map/screenshots/`. הם משקפים את ה-WPF client בזמן ריצה, לאחר התיקונים שתוארו בפרק 17.

### 🟦 דפי הרשמה והתחברות (Auth)

MainWindow — מסך פתיחה

MainWindow

RoleSelection — בחירת תפקיד

RoleSelection

LogIn — מסך התחברות

LogIn

SignUp — הרשמה למשתמש חדש (תלמיד או מורה)

SignUp

ChooseTeacher — בחירת מורה / צפייה בסקירות

ChooseTeacher

Rewiews — רשימת הביקורות של המורה

Rewiews

### 🟩 צד תלמיד (Student)

StudentUI — מסך הבית של התלמיד

StudentUI

ScheduleLesson — קביעת שיעור

ScheduleLesson

ViewLessons — שיעורים קרובים והיסטוריה

ViewLessons

WriteRewiew — כתיבת ביקורת על המורה

WriteRewiew

StudentPayment — תשלום על שיעורים לא משולמים

StudentPayment

StudentNotifications — התראות התלמיד + שליחת הודעות למורה

StudentNotifications

### 🟧 צד מורה (Teacher)

TeacherUI — מסך הבית של המורה (כולל תג ADMIN אם רלוונטי)

TeacherUI

AllStudents — רשימת תלמידים שלי + אישור תלמידים חדשים

AllStudents

Teacher_Schedule — מערכת השעות והשיעורים של המורה

Teacher_Schedule

TeacherPaymentReports — דוחות הכנסות

TeacherPaymentReports

TeacherConfirmPayments — אישור תשלומים

TeacherConfirmPayments

TeacherNotifications — התראות + שליחת הודעות לתלמידים

TeacherNotifications

CalendarTeacher — לוח זמינות

CalendarTeacher

TeacherSettings — הגדרות מחיר ושיטות תשלום

TeacherSettings

StudentPricingManagement — מחיר מותאם / הנחה לפי תלמיד

StudentPricingManagement

### 🟥 צד מנהל (Admin — מורה עם IsAdmin=true)

AdminDashboard

AdminDashboard

AdminTicketManagement — ניהול פניות תמיכה

AdminTicketManagement

AdminUserManagement — ניהול משתמשים, מינוי אדמינים, איפוס סיסמה

AdminUserManagement

AdminTicketDetails — פתיחת פנייה ספציפית

AdminTicketDetails

### ⬜ פיצ'רים משותפים (Shared)

Chat — צ'אט בין משתמשים

Chat

MyTickets — רשימת הפניות שלי

MyTickets

CreateTicket — פתיחת פנייה חדשה

CreateTicket

TicketDetails — צפייה בפנייה ספציפית והתכתבות מולה

TicketDetails

## 21.4 גרסאות

Visual Studio 2022 / 2026.

.NET Framework 4.7.2 (WCF + WPF).

.NET 8 / 10 (Razor Pages + MAUI).

Microsoft Access (ACE OLEDB 12.0).

סוף ספר הפרויקט.