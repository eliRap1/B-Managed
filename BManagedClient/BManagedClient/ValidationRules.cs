using BManagedClient.BMsrv;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BManagedClient
{
    public class AgeRangeRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            int age = 0;
            int min = 1, max = 99;
            try
            {
                if (((string)value).Length > 0)
                    age = Int32.Parse((String)value);
            }
            catch (Exception e)
            {
                return new ValidationResult(false, "Illegal characters or " + e.Message);
            }

            if ((age < min) || (age > max))
            {
                return new ValidationResult(false,
                  "Please enter an age in the range: " + min + " - " + max + ".");
            }
            else
            {
                return ValidationResult.ValidResult;
            }
        }
    }

    public class TeacherIdRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string id = (string)value;
            if (id == "" || id == "-")
            {
                return new ValidationResult(false, "Please enter a legal Teacher id.");
            }
            else
            {
                try
                {
                    int.Parse(id);
                }
                catch
                {
                    return new ValidationResult(false, "Please enter a legal Teacher id.");
                }
                try
                {
                    BMsrv.Service1Client srv = new BMsrv.Service1Client();
                    var user = srv.GetUserById(int.Parse(id));
                    if (user != null)
                    {
                        return ValidationResult.ValidResult;
                    }
                    else
                    {
                        return new ValidationResult(false, "Please enter a legal Teacher id.");
                    }
                }
                catch
                {
                    return new ValidationResult(false, "Please enter a legal Teacher id.");
                }
            }
        }
    }

    public class EmailRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string email = (string)value;
            Regex regex = new Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$");
            Match match = regex.Match(email);
            if (match.Success)
                return ValidationResult.ValidResult;
            else
                return new ValidationResult(false, "Please enter a legal Email.");
        }
    }

    public class PhoneRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string phone = (string)value;
            if (Regex.Match(phone, "^[1-9][0-9]{8}$").Success)
                return ValidationResult.ValidResult;
            else
                return new ValidationResult(false, "Please enter a legal phone.");
        }
    }

    public class isAdminRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string admin = (string)value;
            if (admin == "Student" || admin == "Teacher")
                return ValidationResult.ValidResult;
            else
                return new ValidationResult(false, "Please select a role");
        }
    }

    public class MinLenth : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string str = (string)value;
            if (str.Length >= 4)
            {
                return ValidationResult.ValidResult;
            }
            else
            {
                return new ValidationResult(false,
                          "Password and Username must be at least 4 characters long");
            }
        }
    }

    //Validation rule for lesson price
    public class LessonPriceRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string priceStr = (string)value;

            if (string.IsNullOrEmpty(priceStr))
                return new ValidationResult(false, "Please enter a lesson price");

            if (!int.TryParse(priceStr, out int price))
                return new ValidationResult(false, "Please enter a valid number");

            if (price < 0)
                return new ValidationResult(false, "Price cannot be negative");

            if (price > 10000)
                return new ValidationResult(false, "Price seems too high. Maximum is 10,000");

            return ValidationResult.ValidResult;
        }
    }
}