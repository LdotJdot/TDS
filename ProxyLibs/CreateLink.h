#pragma once
#ifndef __CREATELINK_H__
#define __CREATELINK_H__
#include "pch.h"
#include <windows.h>
#include <shobjidl.h>
#include <objbase.h>
#include <SHLOBJ.H>
#include <exception>

LPCWSTR CharToLPCWSTR(const char* str) {
    // ������ַ��ַ����ĳ���
    int wideCharLen = MultiByteToWideChar(CP_ACP, 0, str, -1, NULL, 0);
    if (wideCharLen <= 0) {
        return NULL; // ת��ʧ��
    }

    // �����ڴ����洢���ַ��ַ���
    wchar_t* wideCharStr = new wchar_t[wideCharLen];

    // ִ��ת��
    MultiByteToWideChar(CP_ACP, 0, str, -1, wideCharStr, wideCharLen);

    // ���ؿ��ַ��ַ���
    return wideCharStr;
}

extern "C"  __declspec(dllexport) int CreateLink(const char* targetPath, const  char* path, const  char* arg, const  char* workDir)
{
    //// ����ϲ�����ַ�������
    //size_t len1 = strlen(targetPath);
    //size_t len2 = strlen(path);
    //size_t len3 = strlen(arg);
    //size_t len4 = strlen(workDir);
    //size_t totalLen = len1 + len2 + len3 + len4;

    //// �����㹻���ڴ����洢�ϲ�����ַ���
    //char* result = new char[totalLen + 1]; // +1 ���ڴ洢�ַ��������� '\0'

    //strcpy_s(result, totalLen + 1, targetPath);
    //strcpy_s(result + len1, totalLen + 1 - len1, path);
    //strcpy_s(result + len1 + len2, totalLen + 1 - len1 - len2, arg);
    //strcpy_s(result + len1 + len2 + len3, totalLen + 1 - len1 - len2 - len3, workDir);

    //// ����ַ���������
    //result[totalLen] = '\0';

    //return result;

    try {
        CoInitialize(NULL);

        IShellLink* psl;
        CoCreateInstance(CLSID_ShellLink, NULL, CLSCTX_INPROC_SERVER, IID_IShellLink, (LPVOID*)&psl);

        psl->SetPath(CharToLPCWSTR(path));
        psl->SetArguments(CharToLPCWSTR(arg));
        psl->SetWorkingDirectory(CharToLPCWSTR(workDir));

        IPersistFile* ppf;
        psl->QueryInterface(IID_IPersistFile, (LPVOID*)&ppf);

        WCHAR wszPath[MAX_PATH];
        MultiByteToWideChar(CP_ACP, 0, targetPath, -1, wszPath, MAX_PATH);
        ppf->Save(wszPath, TRUE);

        ppf->Release();
        psl->Release();

        CoUninitialize();
        return 0;
    }
    catch(const std::exception& e) 
    {
        return -1;
    }
}


#endif
