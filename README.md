## Project Intro

PassGenAI is an example of machine learning being applied to password guessing or password generation. 


## Project Features

* C#
* Hidden Markov Model
* OpenCL Accelerated (GPGPU)
* AI/ML

## Before You Build

You will probably want to setup hashcat so that you can test the results as well as download a list of hashes to crack. Also if you would like to generate your own models you will need a list of clear text passwords to start.

##### Hashcat

<a href="https://github.com/hashcat/hashcat">Hashcat on Github</a>

## Usage Examples

* PassGenAI.exe hmm password_list.txt
* PassGenAI.exe mask password_list.txt "c:\hashcat\hashcat64.exe --session job -O -a 3 -m 100 hashes.txt {0}" > hashcat.bat
* PassGenAI.exe passgen

## Software Licensing Policy

##### For Open Source Projects

If you are developing and distributing open source applications under the GPL License, then you are free to use this project under the GPL License.
<a href="http://www.gnu.org/licenses/gpl-faq.html">GPL FAQ</a>

##### Commercial, Enterprise and Government Projects

Contact me at Landon.Key@gmail.com for more information on Commercial, Enterprise, and Government use of the this project.
