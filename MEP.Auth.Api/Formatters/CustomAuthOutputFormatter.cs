using MEP.Auth.Api.DTOs.Authentication_DTOs;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System;
using System.Text;
using System.Threading.Tasks;

namespace MEP.Auth.Api.Formatters
{
    public class CustomAuthOutputFormatter : TextOutputFormatter
    {
        public CustomAuthOutputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/custom-auth"));
            SupportedEncodings.Add(Encoding.UTF8);
        }

        protected override bool CanWriteType(Type? type)
        {
            return typeof(UserResponseDto).IsAssignableFrom(type);
        }

        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            var response = context.HttpContext.Response;
            var buffer = new StringBuilder();

            if (context.Object is UserResponseDto user)
            {
                buffer.AppendLine($"[AUTH REPORT]");
                // SỬA TẠI ĐÂY: Thêm .ToString() để in chuỗi UUID chuẩn ra text cho đẹp
                buffer.AppendLine($"ID: {user.UserId.ToString()}");
                buffer.AppendLine($"Username: {user.Username}");
                buffer.AppendLine($"Token: {user.Token}");
            }

            await response.WriteAsync(buffer.ToString(), selectedEncoding);
        }
    }
}