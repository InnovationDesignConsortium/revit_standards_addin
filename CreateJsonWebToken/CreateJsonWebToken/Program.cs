
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

try
{
    string private_key = "-----BEGIN RSA PRIVATE KEY-----\r\nMIIEogIBAAKCAQEAvvDXmC98NjjAdYEPhbahhzfxl3EbuiMfuFJR2IBQE4TLw/Ti\r\nVhvhQTn/9ByUsFjfh7gGyG8KhyOXOTVcGL1L49hUWFD+6ecjyiR7CPk1EvQXCfZ+\r\nM2sBTL2ergW//t0eqv9z5GICMY3FIjdk0T5GfPy/6liBlsEAqq3ZouJ7eJ+5/FNL\r\ndsA2EY77OMZDR/ANBSROEZRcQSxsclHE2ERidHQMojrnmcljREueFife/Hha/7xe\r\nkVhnRogWRdnL8cJbQ+6jfnaBPaL//KnFjNSfO6/qq85/OsdpQ14on3sKUggEBkNl\r\n7rVnMJVV7yHhLOtX0raPzbvbJxppqh7GzFnTEQIDAQABAoIBAFqroaVp/zD8WCA0\r\nbjuP0zqTzUyd8I2+eiScKrOFkwEB0YU3N3eue5Pux+WS4OSw/0zCja4GVNiBhSEs\r\nfpRc57nFk5/wrmxCT5OBKU/Ej3h1oq8fdyRRjudzL/PxVQ/gztxivBTambIQYWlz\r\nJpPaX7yghT3yU29ULU3fina31+waEhd3+Sg3+JOQ/zJS+cEmoW5M4+wXd+QcMZUB\r\n5WjOMBh+D9OESe0X5iZ/LsCEePs2TNITaFlGg9LTUByzn8EgLKTyKIWJE2rRIgrp\r\nOzNxmLplLI8TEmrhozRuEhsjjezH+wOXojTlV+wqzsqIaV7WKaUEWU3ZSE+j35pv\r\nLW/RRAECgYEA9he4TScx6GDBrgs1lTqoGsNiS9NP02qCvexZ43mdvP7AxRFS/HNe\r\nO8ZJbiv0LBCnbcVBlkRladjcEd/ycMWuZ8hG+qf97hX1wb1fuHxgDcB8muTpdASb\r\nTB/TujQ7Tai6YRBPtt/NfpfMYLRzzcaHceSLHSWJ5rdotVYGUAvxgdECgYEAxqC3\r\nUYXhCJrI2mo/IqVCue0Tw8Rp1Maa5NzBpKgJvK0RY93mYoe9xhXdfTd1Uk05v/ED\r\nADLVOCrLNeS970hXyMO/szznZ02kqiWQf0jL7TFIZSd2VbboeJguu2/VaGAcBwqP\r\nkT41duKde+0sYL8goWxQl/n/WPESC3jEyEM8TUECgYEAte81VvzKHdUiewxYcdnq\r\nm9ak3g/8LP3KaKTKk6y+nBHu7AJxyqd0HFbsxKGEI+uwDCxP38ry+rzTffeFoi/T\r\nT3C2YOs/hPwBM1lQ4fA5hxEuTck8eoRJV48UFc41paU/HTFU7YspvhR1iWz/TDsg\r\nuWfQHR06hTJFHALcKeOaiXECfxBLHr6RPOR3zgIctREifVbDG9vzQLszj4E2mqvn\r\nHOVdTQ/kJAHxIKAfKwwagIU/0HzuSFC72sHAwOqq2OnIBWtyo0cQt+rBc8CBVFkc\r\nn53VbRrfIdXmKyu5UBwQEHF/cM0jEKPZdolKDaEc04ccJpEXUYUl/MxO+iv2vC2x\r\nVkECgYEAiAfM0OqXepU9PT3ky+btpVtR84wlmOIEqCm/6GAGbiz0srX7A0KX2bhL\r\nfZhKhh9IhRCy488ziKTLELrIiCZAVe9ehq+VcJH0FpITWySfh044CSgZ0Z1UnRVX\r\nju0Cz0+r/DfaqOtK7Sj+NhFJn8SyR1pKbfgKhXHwu7V+aIZs6T0=\r\n-----END RSA PRIVATE KEY-----";
    const string client_id = "Iv23li4ATvi53MDtKsdI";
    var now = DateTime.UtcNow.AddSeconds(-60);
    var rsa = System.Security.Cryptography.RSA.Create();
    rsa.ImportFromPem(private_key);
    var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
    var jwtSecurityTokenHandler = new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false };
    var jwt = jwtSecurityTokenHandler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = client_id,
        Expires = now.AddMinutes(10),
        IssuedAt = now,
        SigningCredentials = signingCredentials
    });
    var tokenString = new JwtSecurityTokenHandler().WriteToken(jwt);
    Console.Write(tokenString);
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}