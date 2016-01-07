require 'rubygems'
require 'open-uri'
require 'nokogiri'
require 'time'

module Pullers
    class DailyStats

        def self.string_to_seconds(time_str)
          (time_str.split(':')[0].to_i * 60) + time_str.split(':')[1].to_i
        end

        def self.run

            yesterday = Date.today - 1.day
            url = "http://www.hockey-reference.com/friv/dailyleaders.cgi?month=#{yesterday.month}&day=#{yesterday.day}&year=#{yesterday.year}"

            Rails.logger.info "##### Pullers::DailyStats.run => Running for #{yesterday}"
            Rails.logger.info "##### Pullers::DailyStats.run => Opening: #{url}"

            page = Nokogiri::HTML(open(url))

            skaters = page.css('#skaters').css('tbody')
            goalies = page.css('#goalies').css('tbody')

            skater_hashes = []
            goalie_hashes = []

            skaters.css('tr').each do |skater_row|

            data_hash = {
                name: skater_row.css('td')[1].content,
                position: skater_row.css('td')[2].content,
                team: skater_row.css('td')[3].content,
                game_location: (skater_row.css('td')[4].content == "" ? "H" : "A"),
                opponent: skater_row.css('td')[5].content,
                game_result: skater_row.css('td')[6].content,
                goals: skater_row.css('td')[7].content,
                assists: skater_row.css('td')[8].content,
                points: skater_row.css('td')[9].content,
                plus_minus: skater_row.css('td')[10].content,
                pim: skater_row.css('td')[11].content,
                goals_ev: skater_row.css('td')[12].content,
                goals_pp: skater_row.css('td')[13].content,
                goals_sh: skater_row.css('td')[14].content,
                goals_gw: skater_row.css('td')[15].content,
                assists_ev: skater_row.css('td')[16].content,
                assists_pp: skater_row.css('td')[17].content,
                assists_sh: skater_row.css('td')[18].content,
                shots: skater_row.css('td')[19].content,
                shot_pct: skater_row.css('td')[20].content,
                shifts: skater_row.css('td')[21].content,
                toi: string_to_seconds(skater_row.css('td')[22].content)
            }

              skater_hashes << data_hash

            end

            goalies.css('tr').each do |goalie_row|

              data_hash = {
                name: goalie_row.css('td')[1].content,
                position: goalie_row.css('td')[2].content,
                team: goalie_row.css('td')[3].content,
                game_location: (goalie_row.css('td')[4].content == "" ? "H" : "A"),
                opponent: goalie_row.css('td')[5].content,
                game_result: goalie_row.css('td')[6].content,
                goalie_decision: goalie_row.css('td')[7].content,
                goals_allowed: goalie_row.css('td')[8].content,
                shots_faced: goalie_row.css('td')[9].content,
                saves: goalie_row.css('td')[10].content,
                save_pct: goalie_row.css('td')[11].content,
                shutout: goalie_row.css('td')[12].content,
                pim: goalie_row.css('td')[13].content,
                toi: string_to_seconds(goalie_row.css('td')[14].content)
              }

              goalie_hashes << data_hash
            end

            goalie_hashes

            Rails.logger.info "##### Pullers::DailyStats.run => Parsing complete"

        end
    end
end
