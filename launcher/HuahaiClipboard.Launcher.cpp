#include <windows.h>
#include <string>
#include <vector>

namespace
{
constexpr wchar_t ActivationEventName[] = L"Local\\HuahaiClipboard.Activate.v1";
constexpr wchar_t ApplicationFileName[] = L"HuahaiClipboard.App.exe";
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR arguments, int)
{
    HANDLE signal = OpenEventW(EVENT_MODIFY_STATE, FALSE, ActivationEventName);
    if (signal != nullptr)
    {
        const BOOL signaled = SetEvent(signal);
        CloseHandle(signal);
        return signaled ? 0 : 3;
    }

    std::vector<wchar_t> modulePath(32768);
    const DWORD pathLength = GetModuleFileNameW(
        nullptr,
        modulePath.data(),
        static_cast<DWORD>(modulePath.size()));
    if (pathLength == 0 || pathLength >= modulePath.size())
        return 4;

    std::wstring directory(modulePath.data(), pathLength);
    const std::wstring::size_type separator = directory.find_last_of(L"\\/");
    if (separator == std::wstring::npos)
        return 5;
    directory.resize(separator);
    const std::wstring applicationPath = directory + L"\\" + ApplicationFileName;

    std::wstring commandLine = L"\"" + applicationPath + L"\"";
    if (arguments != nullptr && arguments[0] != L'\0')
    {
        commandLine += L" ";
        commandLine += arguments;
    }
    std::vector<wchar_t> mutableCommand(commandLine.begin(), commandLine.end());
    mutableCommand.push_back(L'\0');

    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    const BOOL started = CreateProcessW(
        applicationPath.c_str(),
        mutableCommand.data(),
        nullptr,
        nullptr,
        FALSE,
        0,
        nullptr,
        directory.c_str(),
        &startup,
        &process);
    if (!started)
        return 6;

    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return 0;
}
