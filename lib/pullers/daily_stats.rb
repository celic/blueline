require 'open-uri'
require 'time'

module Pullers
    class DailyStats

        def self.run(date = Date.today - 1.day)

            # construct url for date
            url = "http://www.hockey-reference.com/friv/dailyleaders.cgi?month=#{date.month}&day=#{date.day}&year=#{date.year}"

            Rails.logger.info '#####'
            Rails.logger.info "##### Pullers::DailyStats => Running for #{date}"
            Rails.logger.info "##### Pullers::DailyStats => Opening: #{url}"
            Rails.logger.info '#####'

            # gets parsed page
            page = Nokogiri::HTML(open(url))

            # get skater and goalie tables
            skaters = page.css('#skaters').css('tbody')
            goalies = page.css('#goalies').css('tbody')

			if skaters.empty?

				Rails.logger.info '##### Pullers::DailyStats => No stats found'
				Rails.logger.info '#####'

				# skip date
				return
			end

			Rails.logger.info '#####'
            Rails.logger.info "##### Pullers::DailyStats => Parsing players"
            Rails.logger.info '#####'

            skaters.css('tr').each do |skater_row|

                player_info = {
                    key: self.unique_id(skater_row.css('td')[1]),
                    name: skater_row.css('td')[1].content,
                    position: Enums::Position.parse(skater_row.css('td')[2].content),
                    team: Team.by_abbrev(skater_row.css('td')[3].content)
                }

				game_info = {
					team: player_info[:team],
					home: (skater_row.css('td')[4].content != '@'),
                    opponent: Team.by_abbrev(skater_row.css('td')[5].content),
                    decision: Enums::Decision.parse(skater_row.css('td')[6].content),
					date: date
				}

                stats = {
                    goals: skater_row.css('td')[7].content,
                    assists: skater_row.css('td')[8].content,
                    points: skater_row.css('td')[9].content,
                    plusminus: skater_row.css('td')[10].content,
                    pim: skater_row.css('td')[11].content,
                    evg: skater_row.css('td')[12].content,
                    ppg: skater_row.css('td')[13].content,
                    shg: skater_row.css('td')[14].content,
                    gwg: skater_row.css('td')[15].content,
                    eva: skater_row.css('td')[16].content,
                    ppa: skater_row.css('td')[17].content,
                    sha: skater_row.css('td')[18].content,
                    shots: skater_row.css('td')[19].content,
                    shot_percentage: skater_row.css('td')[20].content,
                    shifts: skater_row.css('td')[21].content,
                    toi: skater_row.css('td')[22].attribute('csk').value
                }

				# update game record
				game = self.update_game game_info, stats

				# find player
				player = self.find_player player_info

                # store player stats
                self.store_stats player, game, stats if player.skater?
			end

			Rails.logger.info '#####'
            Rails.logger.info "##### Pullers::DailyStats => Parsing goalies"
            Rails.logger.info '#####'

            goalies.css('tr').each do |goalie_row|

                player_info = {
                    key: self.unique_id(goalie_row.css('td')[1]),
                    name: goalie_row.css('td')[1].content,
                    position: Enums::Position.parse(goalie_row.css('td')[2].content),
                    team: Team.by_abbrev(goalie_row.css('td')[3].content)
                }

				game_info = {
					team: player_info[:team],
					home: (goalie_row.css('td')[4].content != '@'),
                    opponent: Team.by_abbrev(goalie_row.css('td')[5].content),
                    decision: Enums::Decision.parse(goalie_row.css('td')[6].content),
					date: date
				}

                stats = {
                    verdict: Enums::GoalieRecord.parse(goalie_row.css('td')[7].content),
                    goals_against: goalie_row.css('td')[8].content,
                    shots_against: goalie_row.css('td')[9].content,
                    saves: goalie_row.css('td')[10].content,
                    save_percentage: goalie_row.css('td')[11].content,
                    shutout: goalie_row.css('td')[12].content,
                    pim: goalie_row.css('td')[13].content,
                    toi: goalie_row.css('td')[14].attribute('csk').value
                }

				# find game
				game = self.find_game game_info

				# find player
				player = self.find_player player_info

                # store goalie stats
                self.store_stats player, game, stats if player.goalie?
            end

			Rails.logger.info '#####'
            Rails.logger.info "##### Pullers::DailyStats => Updating games"
            Rails.logger.info '#####'

			Game.where(date: date).each do |game|

				# update teams records and points
				self.update_teams game
			end

			Rails.logger.info '#####'
            Rails.logger.info "##### Pullers::DailyStats => Done"
            Rails.logger.info '#####'
        end

        def self.unique_id(row)

            # get unique id
            row.css('a').attribute('href').value.split('/').last.split('.').first
        end

		def self.find_game(info)

			# collect teams
			teams = [ info[:team], info[:opponent] ]

			# determine home and away team
			home, away = info[:home] ? teams : teams.reverse

			# find or create game
			Game.find_or_create_by date: info[:date], home: home, away: away
		end

		def self.find_player(info)

			# find player for key
            player = Player.find_by key: info[:key]

            unless player

                # create new player if one was not found
                player = Player.create info

                Rails.logger.info "##### Pullers::DailyStats => Created #{info[:name]}"
            end

			# return player
			player
		end

		def self.update_game(info, stats)

			# find game
			game = self.find_game info

			# find or create game stat
			game_stats = GameStat.find_or_create_by game: game, team: info[:team], home: info[:home], decision: info[:decision]

			stats.each do |key, value|

				# increment stat value
				game_stats.increment key, value.to_i if game_stats.has_attribute?(key)
			end

			# save increments
			game_stats.save!

			# return game
			game
		end

        def self.store_stats(player, game, stats)

            # create stats record
            player.add_stats! game, stats
        end

		def self.update_teams(game)

			# get winning team
			winner = game.stats.find_by(decision: Enums::Decision::WIN).team

            # get other team
            losing_record = game.stats.find_by('decision != ?', Enums::Decision::WIN)
            loser = losing_record.team

            # regulation finish
            if game.stats.find_by('decision = ?', Enums::Decision::LOSS)
                winner.increment :wins

            # overtime finish
            elsif game.stats.find_by('decision = ?', Enums::Decision::OTL)
                winner.increment :wins

            # shootout finish
            elsif game.stats.find_by('decision = ?', Enums::Decision::SOL)
                winner.increment :sow
            end

			# update record
			winner.increment :points, 2
			winner.save!

			# update record
			loser.increment [ nil, nil, :losses, :ot, :sol ][losing_record.decision]
			loser.increment :points, Enums::Decision.points_for(losing_record.decision)
			loser.save!
		end
    end
end
