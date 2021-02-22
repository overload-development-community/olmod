#ifndef OLMD_CONFIG_H
#define OLMD_CONFIG_H

#include <string>
#include <sstream>
#include <vector>
#include <cstdlib>
#include <cctype>
#include <cstring>

namespace OlmodPlayerDumpState {

class ConfigParam {
	public:
		typedef enum {
			CFG_NONE=0,
			CFG_INT,
			CFG_UINT,
			CFG_FLOAT,
			CFG_STRING,
		} ConfigType;

		ConfigType  type;
		void* data;
		std::string name;
		std::string shortName;

		ConfigParam(ConfigType t, void* ptr, const char *n, const char *shortN=NULL) :
       			type(t),
			data(ptr)
		{
			name=n;
			if (shortN) {
				shortName=shortN;
			}
		}

		ConfigParam(int& var, const char *n, const char *shortN=NULL) :
       			type(CFG_INT),
			data(&var)
		{
			name=n;
			if (shortN) {
				shortName=shortN;
			}
		}

		ConfigParam(unsigned& var, const char *n, const char *shortN=NULL) :
       			type(CFG_UINT),
			data(&var)
		{
			name=n;
			if (shortN) {
				shortName=shortN;
			}
		}

		ConfigParam(float& var, const char *n, const char *shortN=NULL) :
       			type(CFG_FLOAT),
			data(&var)
		{
			name=n;
			if (shortN) {
				shortName=shortN;
			}
		}

		bool FromStr(const char *str)
		{
			int *iptr;
			unsigned *uptr;
			float *fptr;
			bool success = true;

			switch(type) {
				case CFG_INT:
					iptr=(int*)data;
					iptr[0]=(int)std::strtol(str,NULL,0);
					break;
				case CFG_UINT:
					uptr=(unsigned*)data;
					uptr[0]=(unsigned)std::strtoul(str,NULL,0);
					break;
				case CFG_FLOAT:
					fptr=(float*)data;
					fptr[0]=(float)std::strtod(str,NULL);
					break;
				default:
					success = false;
			}
			return success;
		}

		bool AcceptFromString(const char *n, const char *str)
		{
			if (!n || !n[0]) {
				return false;
			}
			if (std::strcmp(n,name.c_str()) && std::strcmp(n,shortName.c_str())) {
				return false;
			}
			if (!str) {
				return false;
			}
			return FromStr(str);
		}

		bool ToStr(std::stringstream& s) const
		{
			const int *iptr;
			const unsigned *uptr;
			const float *fptr;
			bool success = true;

			switch(type) {
				case CFG_INT:
					iptr=(const int*)data;
					s << iptr[0];
					break;
				case CFG_UINT:
					uptr=(const unsigned*)data;
					s << uptr[0];
					break;
				case CFG_FLOAT:
					fptr=(const float*)data;
					s << fptr[0];
					break;
				default:
					s << "!ERROR!";
					success = false;
			}
			return success;

		}

		std::string ToStr() const
		{
			std::stringstream s;
			ToStr(s);
			return s.str();
		}

		bool ToConfigStr(std::stringstream& s) const
		{
			bool success;
			s << name << "=";
			success = ToStr(s);
			s << ";";
			return success;
		}

		bool ToShortStr(std::stringstream& s) const
		{
			bool success;
			s << (shortName.empty()?name:shortName);
			success = ToStr(s);
			return success;
		}

};

class Config {
	protected:
		std::vector<ConfigParam> params;
		typedef enum {
			PM_SEARCH_NAME=0,
			PM_NAME,
			PM_SEARCH_DELIM,
			PM_SEARCH_VALUE,
			PM_VALUE,
			PM_END
		} ParseMode;

	public:
		void Add(const ConfigParam& p)
		{
			params.push_back(p);
		}

		void Parse(const char *cfg)
		{
			size_t i=0;
			char c;
			std::string name;
			std::string value;
			ParseMode pm = PM_SEARCH_NAME;

			while( (c=cfg[i]) ) {
				int v = (int)((unsigned char)c);
				switch (pm) {
					case PM_SEARCH_NAME:
						if (std::isalnum(v)) {
							name = name + c;
							pm = PM_NAME;
						} 
						break;
					case PM_NAME:
						if (std::isalnum(v)) {
							name = name + c;
						} else {
							if (c == '=') {
								pm = PM_SEARCH_VALUE;
							} else {
								pm = PM_SEARCH_DELIM;
							}
						}	
						break;
					case PM_SEARCH_DELIM:
						if (c == '=') {
							pm = PM_SEARCH_VALUE;
						}
						break;
					case PM_SEARCH_VALUE:
						if (c == ';') {
							pm = PM_END;
						} else if (!std::isspace(v)) {
							value += c;
							pm = PM_VALUE;
						}
						break;
					case PM_VALUE:
						if (c == ';') {
							pm = PM_END;
						} else if (!std::isspace(v)) {
							value += c;
							pm = PM_VALUE;
						}
						break;
					case PM_END:
						(void)0;
				}
				if (pm == PM_END) {
					for (size_t j=0; j<params.size(); j++) {
						if (params[j].AcceptFromString(name.c_str(),value.c_str())) {
							break;
						}
					}
					name.clear();
					value.clear();
					pm=PM_SEARCH_NAME;
				}
				i++;
			}
		}

		void GetCfg(std::stringstream& s) const
		{
			for (size_t i=0; i<params.size(); i++) {
				params[i].ToConfigStr(s);
			}
		}

		void GetShortCfg(std::stringstream& s, bool addPrefixDelim = false) const
		{
			for (size_t i=0; i<params.size(); i++) {
				if ( (i > 0) || addPrefixDelim) {
					s << "_";
				}
				params[i].ToShortStr(s);
			}
		}

		std::string GetCfg() const
		{
			std::stringstream s;
			GetCfg(s);
			return s.str();
		}




};

} // namespace OlmodPlayerDumpState 
#endif // !OLMD_CONFIG_H
